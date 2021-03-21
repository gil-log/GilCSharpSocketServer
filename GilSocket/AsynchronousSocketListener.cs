using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Collections.Generic;

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

        public static Dictionary<string, StateObject> socketDic;

        // 쓰레드 신호가 false이므로 일단 쓰레드가 시작 안된다.
        // allDone.set() 해주어야 대기중인 쓰레드들이 시작된다.
        // 쓰레드들간 통신 기능 담당
        public static ManualResetEvent allDone = new ManualResetEvent(false);

        public AsynchronousSocketListener() { }

        public static void ViewAvailableThreadsAtMoment(string moment)
        {
            int worker = 0;
            int asyncIO = 0;
            
            ThreadPool.GetAvailableThreads(out worker, out asyncIO);

            Console.WriteLine("[Available][{0}] Worker Threads = {1}, Max AsyncIO Threads = {2}", moment, worker, asyncIO);
        }

        public static void ViewMemoryAtMoment(string moment)
        {
            long threadMemory = GC.GetAllocatedBytesForCurrentThread();
            GCMemoryInfo gcInfo = GC.GetGCMemoryInfo();
            long totalMemory = GC.GetTotalMemory(false);
            Console.WriteLine("[Memory][{0}] allocatedbytesAtThread = {1}, totalMemory = {2}", moment, threadMemory, totalMemory);


        }

    public static void StartListening()
        {
            // 쓰레드 확인 시작
            Thread curThread = Thread.CurrentThread;

            Console.WriteLine("[StartListening] current thread id = {0}, hascode = {1}", curThread.ManagedThreadId, curThread.GetHashCode());
            // 쓰레드 확인 끝


            // 소켓을 위한 로컬 엔드포인트를 승인해준다.
            // 컴퓨터의 dns 이름
            IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
            //IPAddress ipAddress = ipHostInfo.AddressList[0];
            IPAddress ipAddress = IPAddress.Parse("0.0.0.0");
            IPEndPoint localEndPoint = new IPEndPoint(ipAddress, 11000);

            Console.Write("server ip = {0} \n", ipAddress.ToString());


            int workerThreads;
            int portThreads;

            ThreadPool.GetMaxThreads(out workerThreads, out portThreads);

            Console.WriteLine("[Max] Worker Threads = {0}, Max AsyncIO Threads = {1}", workerThreads, portThreads);
            ViewAvailableThreadsAtMoment("Socket Create");
            ViewMemoryAtMoment("Socket Create");

            // TCP 소켓 생성
            Socket listener = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            // 로컬 엔드 포인트에 소켓을 바인딩 해주고, 들어올 연결을 listen 한다.
            try
            {
                // 위에서 생성한 로컬 엔드 포인트(서버 컴퓨터 IP, 포트 11000)와 소켓을 연결한다.
                listener.Bind(localEndPoint);
                //ViewAvailableThreadsAtMoment("Socket Bind");
                // 소켓을 수신 상태로 둔다. 연결 큐의 최대 길이로 100을 둔다.
                // 100개 까지 수신 큐에 대기 가능
                listener.Listen(100);
                //ViewAvailableThreadsAtMoment("Socket listen");

                while (true)
                {
                    // 스레드 초기화로 이벤트를 논신호 상태로
                    // 동작 중인 스레드가 멈춘다.
                    ViewAvailableThreadsAtMoment("thread reset");
                    allDone.Reset();


                    // 연결 대기 listen하는 비동기 소켓을 시작한다.
                    Console.WriteLine("wating for a connection ...");
                    // callback 메소드에서 EndAccept()을 실행해야된다.
                    // 비동기 콜백함수를 통해 다른 쓰레드에서 실행 되게 함,?!?!
                    // 신호 들어올 시 AcceptCallback 실행
                    // 비동기 작업 = 소켓 listener가 클라이언트 소켓의 연결을 수립 해주는것.
                    // 그 비동기 작업 이후에 콜백 메소드가 실행 된다.
                    // listenr는 AcceptCallback 함수의 매개변수로 사용된다.

                    //BeginXXX()가 비동기 작업 완료를 위해 쓰레드를 시작한다.
                    // 이 쓰레드가 AsyncCallback을 만든다.
                    // BeginXXX() 메소드들은 애초에 다른 스레드를 사용.

                    // ThreadPool에서 기본 소켓 I/O 작업 완료를 위해 발생한 임의 스레드에서 호출 된다.


                    // 비동기 호출은 쓰레드 풀 안에서 사용된다.



                    // 메인 쓰레드 > BeginAccept 까지만 비동기 쓰레드풀 생성 -> 이후 listener state로 이 한 쓰레드에서
                    // 요청 처리 - 해제 까지 동기로?

                    // thread - safe로 아는데, 애초에 BeginAccept -> EndAccept -> Recevie, Send 사용 가능??

                    // 콜백 함수를 호출하는 비동기 메소드가 생성하는 쓰레드에서( BeginXXX() )
                    // 이상적으로는 이 동일 스레드에서 요청을 처리하고, 요청 처리 CPU 바운드 작업 완료 이후 스레드를 해제 하는 것이 좋다.

                    // 일박ㅇ적으로 CPU 바운드 작업은 스레드 풀 스레드를 사용한다.

                    GC.Collect();
                    listener.BeginAccept(new AsyncCallback(AcceptCallback), listener);

                    //Thread afterThread = Thread.CurrentThread;
                    //Console.WriteLine("[AfterBeginAccept] current thread id = {0}, hascode = {1}", afterThread.ManagedThreadId, afterThread.GetHashCode());

                    ViewAvailableThreadsAtMoment("begin Aceept");
                    ViewMemoryAtMoment("begin Aceept");
                    // 하나의 연결이 완료될때 까지 대기한다
                    // 연결이 일어나면 바로 다음 연결을 기다린다.
                    // BeginAccept 실행 하는 쓰레드가 여기서 비동기 구문이 끝날 때 까지 기다린다.
                    // just like 여기다 while(true) 걸어 놓는거랑 비슷
                    // 비동기 작업 = 소켓 linstener가 클라이언트 소켓 연결 수립 해주는게 끝나기 전까지
                    // allDone.Reset() -> Console.WriteLine -> listener.BeginAccept() 을 무한 반복 한다.??
                    // WaitOne() 이전 함수들을 무한 반복한다.??
                    allDone.WaitOne();
                    ViewAvailableThreadsAtMoment("wait One");
                    ViewMemoryAtMoment("wait One");

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
            // 다시 차단기 올리는 셈
            // 
            allDone.Set();


            GC.Collect();

            // 쓰레드 확인 시작
            Thread curThread = Thread.CurrentThread;
            Console.WriteLine("[AcceptCallback] current thread id = {0}, hascode = {1}", curThread.ManagedThreadId, curThread.GetHashCode());
            ViewMemoryAtMoment("AcceptCallback");


            // 쓰레드 확인 끝

            /*            // 쓰레드 확인 시작
                        Thread curThread = Thread.CurrentThread;

                        Process proc = Process.GetCurrentProcess();
                        ProcessThreadCollection ptc = proc.Threads;


                        int i = 1;

                        foreach (ProcessThread pt in ptc)
                        {
                            Console.WriteLine("******* {0} 번째 스레드 정보 *******", i++);
                            Console.WriteLine("ThreadId : {0}", pt.Id);            //스레드 ID
                            Console.WriteLine("시작시간 : {0}", pt.StartTime);    //스레드 시작시간
                            Console.WriteLine("우선순위 : {0}", pt.BasePriority);  //스레드 우선순위
                            Console.WriteLine("상태 : {0}", pt.ThreadState);      //스레드 상태
                            Console.WriteLine();
                        }
                        Console.WriteLine("현재 프로세스에서 실행중인 스레드 수 : {0}", ptc.Count);
                        Console.WriteLine("current thread id = {0}", curThread.ManagedThreadId);
                        // 쓰레드 확인 끝*/


            // 클라이언트 요청의 소켓 핸들러를 가져온다.
            Socket listener = (Socket)ar.AsyncState;
            // 원격 호스트와 클라이언트의 연결시도를 비동기적으로 수립,
            // 데이터 송수신이 가능한 새 소켓 객체 반환.
            Socket handler = listener.EndAccept(ar);

            // 상태 객체 생성
            // 위에서 생성한 상태 객체, 버퍼 사이즈, 바이트 타입 버퍼, StringBuilder가 선언된 Class.
            StateObject state = new StateObject();

            // 원격 호스트 소켓과 데이터 송수신이 가능한 hadnler 클라이언트 소켓 객체를
            // state 클래스 socket 타입 workSocket에 저장한다.
            state.workSocket = handler;

            // 매개 변수 = 저장할 버퍼, 수신 받은 버퍼를 저장할 인덱스 시작 위치(0), 버퍼 사이즈, 버퍼 플래그(0은 flag 아무것도 사용 안함), 콜백 함수, 상태 객체
            // 연결된 소켓에 비동기적 수신 시작.
            // 비동기 콜백함수를 통해 다른 쓰레드에서 실행 되게 함???
            // -> new AsyncCallback을 통해서 별도의 쓰레드에서 실행이 되고, 마지막 매개 변수로 주는 state 상태 개체를 통해서
            // 비동기 메소드와 콜백 함수 사이에 정보 전달을 해줄 수 있다.

            // 콜백 함수에는 EndReceive를 호출 해야 한다.(일반적)
            // 비동기 작업(BeginReceive)이 종료되면 ReadCallback을 콜백 함수로 실행한다.
            handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReadCallback), state);



        }

        public static void ReadCallback(IAsyncResult ar)
        {


            // 쓰레드 확인 시작
            try {
                Thread curThread = Thread.CurrentThread;
                Console.WriteLine("[ReadCallback] current thread id = {0}, hascode = {1}", curThread.ManagedThreadId, curThread.GetHashCode()); 
            }
            catch(Exception e) {
                Console.WriteLine(e.ToString());
            }
            
            // 쓰레드 확인 끝

            ViewAvailableThreadsAtMoment("ReadCallback");

            String content = String.Empty;

            // 비동기 상태 객체에서 소켓 핸들러와 상태 객체를 찾는다
            StateObject state = (StateObject)ar.AsyncState;
            Socket handler = state.workSocket;

            // 클라이언트 소켓에서 받은 데이터 크기를 저장한다.
            int bytesRead = handler.EndReceive(ar);

            if (bytesRead > 0)
            {
                // 클라이언트 소켓 객체로 부터 받은 byte buffer를 ASCII로 인코딩해서 상태객체 state의 StringBuilder에 append 한다.
                state.sb.Append(Encoding.ASCII.GetString(state.buffer, 0, bytesRead));

                // 상태 객체 state의 StringBuilder를 String으로 변환해서 Content에 저장한다.
                content = state.sb.ToString();
                // <EOF>의 index가 있으면 클라이언트 소켓 객체 handler와 수신받아 string으로 변환한 content를 가지고 send()를 실행한다.
                if (content.IndexOf("<EOF>") > -1)
                {
                    Console.WriteLine("Read {0} bytes from socket. \n Data : {1}", content.Length, content);
                    // 클라이언트에게 데이터를 다시 던져준다.
                    Send(handler, content);
                }
                // <EOF> index가 없으면 재귀 함수 처럼 다시 수신을 시작한다.
                else
                {
                    // EOF를 만나지 않아 계속 수신
                    handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReadCallback), state);
                }
            }
        }

        private static void Send(Socket handler, String data)
        {
            // 클라이언트로 받은 데이터를 가지고 form.exe를 process에서 실행한후 결과값을 return한다.
            String transferData = TransferData(data);

            // Stirng data를 Byte Data로 전환한다. ASCII 인코딩을 사용
            byte[] byteData = Encoding.ASCII.GetBytes(transferData);

            // 클라이언트에 데이터를 보낸다.
            // 매개변수는 보낼 byte[] 데이터, 보낼 buffer의 index(0), 버퍼 크기, socket flag(0은 flag 안씀), 비동기 콜백함수를 통해 다른 쓰레드에서 실행 되게 함, 
            handler.BeginSend(byteData, 0, byteData.Length, 0, new AsyncCallback(SendCallback), handler);
        }

        private static String TransferData(String data)
        {
            int eofIndex = data.IndexOf("<EOF>");

            String subData = data.Substring(0, eofIndex);

            String command = "GilSocketResultForm " + subData;

            ProcessStartInfo pri = new ProcessStartInfo();
            Process pro = new Process();

            /*            pri.FileName = "cmd.exe";
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
                        pro.Close();*/

            pri.FileName = "C:/Users/Gillog/source/repos/GilSocketResultForm/GilSocketResultForm/bin/Debug/GilSocketResultForm.exe";
            pri.UseShellExecute = false;
            pri.RedirectStandardOutput = true;
            pri.RedirectStandardInput = true;

            pri.Arguments = subData;

            pro.StartInfo = pri;

            pro.Start();

            //pro.StandardInput.Write(subData);
            //pro.StandardInput.Close();

            String resultValue = pro.StandardOutput.ReadToEnd();
            pro.WaitForExit();
            pro.Close();


            /*

                        Regex reg = new Regex(@"GilSocketResultForm\s+[0-9]+\s+<send>([0-9]+)[^0-9]");
                        MatchCollection matchColl = reg.Matches(resultValue);

                        //Console.WriteLine("beforemathed = {0}", resultValue);


                        foreach (Match matched in matchColl)
                        {
                            resultValue = matched.Groups[1].Value;
                            //Console.WriteLine("mathed = {0}", resultValue);

                        }
            */
            //int sendIndex = resultValue.IndexOf("send:");
            //String result = resultValue.Substring(sendIndex, resultValue.Length);

            Regex reg = new Regex(@"<send>([0-9]+)");
            MatchCollection matchColl = reg.Matches(resultValue);

            //Console.WriteLine("beforemathed = {0}", resultValue);


            foreach (Match matched in matchColl)
            {
                resultValue = matched.Groups[1].Value;
                //Console.WriteLine("mathed = {0}", resultValue);

            }

            Console.WriteLine("exe result = {0}", resultValue);

            return resultValue;
        }

        private static void SendCallback(IAsyncResult ar)
        {
            // 쓰레드 확인 시작
            Thread curThread = Thread.CurrentThread;
            Console.WriteLine("[SendCallback] current thread id = {0}, hascode = {1}", curThread.ManagedThreadId, curThread.GetHashCode());
            ViewMemoryAtMoment("SendCallback");
            // 쓰레드 확인 끝

            try
            {
                // 소켓으로 부터 상태 객체를 가져온다.
                Socket handler = (Socket)ar.AsyncState;

                // 원격 디바이스에 데이터 전송을 완료한다.
                // 보낸 byte 수를 return 한다. 아니면 오류를 return
                int bytesSent = handler.EndSend(ar);
                Console.WriteLine("Sent {0} bytes to client.", bytesSent);

                // 클라이언트 소켓 handler의 송수신을 차단한다.
                handler.Shutdown(SocketShutdown.Both);
                // 클라이언트 소켓 handler의 연결을 닫고 리소스를 해제한다.
                handler.Close();
                //curThread.Interrupt();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }

            // 스레드 반환 해야하는데,,,


            finally
            {

                ViewAvailableThreadsAtMoment("SendCallback Finally");
                ViewMemoryAtMoment("SendCallback Finally");

                Console.WriteLine("end!!!!!");
                Console.WriteLine();
            }

        }

        public static void Main(String[] args)
        {
            // 쓰레드 확인 시작
            Thread curThread = Thread.CurrentThread;
            Console.WriteLine("[Main] current thread id = {0}, hascode = {1}", curThread.ManagedThreadId, curThread.GetHashCode());
            // 쓰레드 확인 끝

            Thread overWatch = new Thread(new ThreadStart(WatchThread));
            overWatch.Start();

            StartListening();
            Console.WriteLine("Listening Started...");
            //return 0;
        }

        public static void WatchThread()
        {
            if (allDone.WaitOne())
            {
                // 쓰레드 확인 시작
                Thread curThread = Thread.CurrentThread;
                Console.WriteLine("[WatchThread] current thread id = {0}, hascode = {1}", curThread.ManagedThreadId, curThread.GetHashCode());
                // 쓰레드 확인 끝
            }
        }
    }
}