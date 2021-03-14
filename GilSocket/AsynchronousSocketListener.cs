using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace GilSocket
{

    public class StateObject
    {
        // client socket
        public Socket workSocket = null;
        // receive buffer size
        public const int BufferSize = 1024;
        // receive buffer
        public byte[] buffer = new byte[BufferSize];
        // received data string
        public StringBuilder sb = new StringBuilder();
    }

    public class AsynchronousSocketListener
    {
        // 쓰레드 신호
        public static ManualResetEvent allDone = new ManualResetEvent(false);

        public AsynchronousSocketListener() { }

        public static void StartListening()
        {
            // 소켓을 위한 로컬 엔드포인트를 승인해준다.
            // 컴퓨터의 dns 이름
            IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
            //IPAddress ipAddress = ipHostInfo.AddressList[0];
            IPAddress ipAddress = IPAddress.Parse("0.0.0.0");
            IPEndPoint localEndPoint = new IPEndPoint(ipAddress, 11000);

            Console.Write(ipAddress.ToString());

            // TCP 소켓 생성
            Socket listener = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            // 로컬 엔드 포인트에 소켓을 바인딩 해주고, 들어올 연결을 listen 한다.
            try
            {
                listener.Bind(localEndPoint);
                listener.Listen(100);

                while (true)
                {
                    // 스레드 초기화로 이벤트를 논신호 상태로
                    allDone.Reset();

                    // 연결 대기 listen하는 비동기 소켓을 시작한다.
                    Console.WriteLine("wating for a connection ...");
                    listener.BeginAccept(new AsyncCallback(AcceptCallback), listener);

                    // 하나의 연결이 완료될때 까지 대기한다
                    // 연결이 일어나면 바로 다음 연결을 기다린다.
                    allDone.WaitOne();








                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }

            Console.WriteLine("\nPress Enter to continue...");
            Console.Read();
        }

        public static void AcceptCallback(IAsyncResult ar)
        {
            // 하나 이상의 대기 중 쓰레드를 동작하기 위해 이벤트 상태를 킨다
            allDone.Set();

            // 클라이언트 요청의 소켓 핸들러를 가져온다.
            Socket listener = (Socket)ar.AsyncState;
            Socket handler = listener.EndAccept(ar);

            // 상태 객체 생성
            StateObject state = new StateObject();
            state.workSocket = handler;
            handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReadCallback), state);

        }

        public static void ReadCallback(IAsyncResult ar)
        {
            String content = String.Empty;

            // 비동기 상태 객체에서 소켓 핸들러와 상태 객체를 찾는다
            StateObject state = (StateObject)ar.AsyncState;
            Socket handler = state.workSocket;

            // 클라이언트 소켓에서 받은 데이터를 읽는다.
            int bytesRead = handler.EndReceive(ar);

            if (bytesRead > 0)
            {
                state.sb.Append(Encoding.ASCII.GetString(state.buffer, 0, bytesRead));

                // 파일 전송의 끝을 찾는다. EOF가 아니면 계속 읽는다.
                content = state.sb.ToString();
                if (content.IndexOf("<EOF>") > -1)
                {
                    Console.WriteLine("Read {0} bytes from socket. \n Data : {1}", content.Length, content);
                    // 클라이언트에게 데이터를 다시 던져준다.
                    Send(handler, content);
                }
                else
                {
                    // EOF를 만나지 않아 계속 수신
                    handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReadCallback), state);
                }
            }
        }

        private static String TransferData(String data)
        {
            int eofIndex = data.IndexOf("<EOF>");

            String subData = data.Substring(0, eofIndex);

            String command = "GilSocketResultForm " + subData;

            ProcessStartInfo pri = new ProcessStartInfo();
            Process pro = new Process();

            pri.FileName = "cmd.exe";
            pri.CreateNoWindow = true;
            pri.UseShellExecute = false;

            pri.RedirectStandardOutput = true;
            pri.RedirectStandardInput = true;
            pri.RedirectStandardError = true;

            pro.StartInfo = pri;
            pro.Start();

            pro.StandardInput.Write(@"cd C:\Users\Gillog\source\repos\GilSocketResultForm\GilSocketResultForm\bin\Debug" + Environment.NewLine);
            pro.StandardInput.Write(@command + Environment.NewLine);
            pro.StandardInput.Close();
            String resultValue = pro.StandardOutput.ReadToEnd();
            pro.WaitForExit();
            pro.Close();

            Regex reg = new Regex(@"GilSocketResultForm\s+[0-9]+\s+<send>([0-9]+)[^0-9]");
            MatchCollection matchColl = reg.Matches(resultValue);

            Console.WriteLine("beforemathed = {0}", resultValue);


            foreach (Match matched in matchColl)
            {
                resultValue = matched.Groups[1].Value;
                Console.WriteLine("mathed = {0}", resultValue);

            }

            //int sendIndex = resultValue.IndexOf("send:");
            //String result = resultValue.Substring(sendIndex, resultValue.Length);


            Console.WriteLine("exe result = {0}", resultValue);

            return resultValue;
        }

        private static void Send(Socket handler, String data)
        {
            String transferData = TransferData(data);

            // Stirng data를 Byte Data로 전환한다. ASCII 인코딩을 사용
            byte[] byteData = Encoding.ASCII.GetBytes(transferData);

            // 원격 디바이스에 데이터를 보낸다.
            handler.BeginSend(byteData, 0, byteData.Length, 0, new AsyncCallback(SendCallback), handler);
        }

        private static void SendCallback(IAsyncResult ar)
        {
            try
            {
                // 소켓으로 부터 상태 객체를 가져온다.
                Socket handler = (Socket)ar.AsyncState;

                // 원격 디바이스에 데이터 전송을 완료한다.
                int bytesSent = handler.EndSend(ar);
                Console.WriteLine("Sent {0} bytes to client.", bytesSent);

                handler.Shutdown(SocketShutdown.Both);
                handler.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        public static void Main(String[] args)
        {
            StartListening();
            Console.WriteLine("Listening Started...");
            //return 0;
        }
    }
}