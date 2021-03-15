using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text;

namespace GilSocket
{
    class Class1
    {
/*        static void Main(string[] args)

        {

            Process proc = Process.GetCurrentProcess();

            ProcessThreadCollection ptc = proc.Threads;

            Console.WriteLine("현재 프로세스에서 실행중인 스레드 수 : {0}", ptc.Count);

            ThreadInfo(ptc);

        }*/



        private static void ThreadInfo(ProcessThreadCollection ptc)

        {

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

        }
    }
}
