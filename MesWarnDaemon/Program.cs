using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MesWarnDaemon
{
    internal static class Program
    {
        /// <summary>
        /// 해당 애플리케이션의 주 진입점입니다.
        /// </summary>
        [STAThread]
        static void Main()
        {
            try
            {
                if (IsRunningAppCheck() == false)
                {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.Run(new Form1());
                }
                else
                {
                    //MessageBox.Show("이미 프로그램이 실행 중 입니다.");
                }
            }
            catch (Exception ex) { }
        }

        /// <summary>
        /// 프로그램의 실행여부를 확인하는 함수.
        /// </summary>
        /// <returns></returns>
        public static bool IsRunningAppCheck()
        {
            System.Diagnostics.Process Process = System.Diagnostics.Process.GetCurrentProcess();
            string ProcName = Process.ProcessName;

            if (System.Diagnostics.Process.GetProcessesByName(ProcName).Length > 1)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
