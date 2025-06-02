using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlClient;
using System.Windows.Forms;
using EasyModbus;
using System.Threading;
using System.Net.Sockets;
using System.IO;
using System.Diagnostics;

namespace MesWarnDaemon
{
    public partial class Form1 : Form
    {
        private string[] spoolUnloadMsg = {
            "Error codes that occur during spool unloading...",
            "[601] 실린더, 브릿지, 스톱바가 초기위치에 있지 않습니다.",
            "[602] 이재할 스풀 갯수가 모자랍니다.",
            "[603] 브릿지 하강 중에 에러가 발생했습니다.",
            "[604] 스풀 2개 중 한개만 이재 체크 중 에러가 발생했습니다.",
            "[605] 스톱바 하강 중에 에러가 발생했습니다.",
            "[606] 실린더 상승 중에 에러가 발생했습니다.",
            "[607] 스풀 이재 중에 에러 등으로 인해 이재가 완료되지 않았습니다.",
            "[608] 브릿지 걸림센서 체크 중에 에러가 발생했습니다.",
            "[609] 브릿지 상승 중에 에러가 발생했습니다.",
            "[610] 실린더 하강 중에 에러가 발생했습니다.",
            "[611] 스톱바 상승 중에 에러가 발생했습니다."
        };

        private string[] spoolLoadMsg = {
            "Error codes that occur during spool loading...",
            "[651] 실린더 하강 중에 에러가 발생했습니다.",
            "[652] 스톱바 상승 중에 에러가 발생했습니다.",
            "[653] 적재센서 체크 결과 적재 준비가 되어있지 않습니다.",
            "[654] 광통신 연결 중 에러가 발생했습니다.",
            "[655] 광통신: 도착 수신이 되지 않습니다.",
            "[656] 브릿지 하강 중에 에러가 발생했습니다.",
            "[657] 광통신: 스풀 적재준비 완료 수신이 되지 않습니다.",
            "[658] 광통신: 스풀 공급장치의 이재 완료 수신이 되지 않습니다.",
            "[659] 스풀 적재 중에 에러 등으로 인해 적재가 완료되지 않았습니다.",
            "[660] 브릿지 걸림센서 체크 중에 에러가 발생했습니다.",
            "[661] 브릿지 상승 중에 에러가 발생했습니다."
        };

        private string[] productUnloadMsg = {
            "Error codes that occur during product unloading...",
            "[701] 실린더, 브릿지, 푸셔가 초기위치에 있지 않습니다.",
            "[702] 광통신 연결 중 에러가 발생했습니다.",
            "[703] 광통신: 도착 수신이 되지 않습니다.",
            "[704] 광통신: 제품 이재준비 완료 수신이 되지 않습니다.",
            "[705] 이재할 제품이 확인되지 않거나, 제품이 적재되어 있지 않습니다.",
            "[706] 브릿지 하강 중에 에러가 발생했습니다.",
            "[707] 실린더 상승 중에 에러가 발생했습니다.",
            "[708] 제품이 제품 이재가 되지 않아 AMR의 적재센서에 제품이 감지되고 있습니다",
            "[709] 브릿지 걸림센서 체크 중에 에러가 발생했습니다.",
            "[710] 브릿지 상승 중에 에러가 발생했습니다.",
            "[711] 실린더 하강 중에 에러가 발생했습니다.",
            "[712] 광통신: 제품 이재 완료 수신이 되지 않습니다."
        };

        private string[] productLoadMsg = {
            "Error codes that occur during product loading...",
            "[751] 실린더 하강 중에 에러가 발생했습니다.",
            "[752] 푸셔 후진 중에 에러가 발생했습니다.",
            "[753] 적재센서 체크 결과 적재 준비가 되어있지 않습니다.",
            "[754] 실린더 중간 상승 중 에러가 발생했습니다.",
            "[755] 브릿지 하강 중에 에러가 발생했습니다.",
            "[756] 푸셔 전진 중에 에러가 발생했습니다.",
            "[757] 제품 적재센서 체크 결과 적재가 제대로 되지 않았습니다.",
            "[758] 브릿지 걸림센서 체크 중에 에러가 발생했습니다.",
            "[759] 푸셔 후진 중에 에러가 발생했습니다.",
            "[760] 실린더 하강 중 에러가 발생했습니다.",
            "[761] 브릿지 상승 중에 에러가 발생했습니다."
        };

        private ModbusClient modbusClient;
        private int[] operationStatus = new int[2];
        private int[] oldStatus = new int[2] { 9, 0 };
        private bool[] blockedStatus = new bool[8];
        private bool[] oldBlocked = new bool[8] {true, true, true, true, true, true, true, true };
        private bool[] onlineStatus = new bool[3];
        private bool[] oldOnline = new bool[3] { true, true, true };
        private SqlConnection conn = new SqlConnection();
        //private SqlConnection conn2 = new SqlConnection();
        private int modbusAddr = int.Parse(System.Configuration.ConfigurationManager.AppSettings["WorkStateAddress"]);
        private int onlineAddr = int.Parse(System.Configuration.ConfigurationManager.AppSettings["OnlineStateAddress"]);
        private int blkAddr = int.Parse(System.Configuration.ConfigurationManager.AppSettings["BlockStateAddress"]);
        private int amrId = int.Parse(System.Configuration.ConfigurationManager.AppSettings["AmrID"]);
        private string workingTaskId = "None";
        private bool dataBase = bool.Parse(System.Configuration.ConfigurationManager.AppSettings["DataBase"]);
        private bool isGoing = false;
        private bool isGoingPrev = true;

        public Form1()
        {
            InitializeComponent();

            this.Text = System.Configuration.ConfigurationManager.AppSettings["AmrName"] + " Alarm & Stop Monitor";

            modbusClient = new ModbusClient(System.Configuration.ConfigurationManager.AppSettings["ModbusServer"], 502);
            conn.ConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["connString"].ConnectionString;
            //conn2.ConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["connString2"].ConnectionString;

            textBox1.Text = System.Configuration.ConfigurationManager.AppSettings["AmrName"];
        }

        private void AddBlockedStatus(bool[] blocked)
        {
            conn.Close();
            conn.Open();
            SqlCommand Comm = new SqlCommand();
            Comm.Connection = conn;

            string strRecord;

            strRecord = string.Format("INSERT INTO tbSingle_BlockedStatus (created_at, amr_id, pause_front, pause_rear, pause_front_IR, pause_rear_IR, stop_bumper, stop_emergency, stop_front, loaded) " +
                    "VALUES (GetDate(), {0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8})", amrId, 
                    blocked[1] ? 1 : 0, blocked[2] ? 1 : 0, blocked[3] ? 1 : 0, blocked[4] ? 1 : 0, blocked[5] ? 1 : 0, blocked[6] ? 1 : 0, blocked[0] ? 1 : 0, blocked[7] ? 1 : 0);
            Comm.CommandText = strRecord;

            Comm.ExecuteNonQuery();
            conn.Close();
        }

        private void AddAlarm(int errorCode)
        {
            conn.Close();
            conn.Open();
            //conn2.Open();
            SqlCommand Comm = new SqlCommand();
            //SqlCommand Comm2 = new SqlCommand();
            Comm.Connection = conn;
            //Comm2.Connection = conn2;

            Comm.CommandText = "SELECT TOP (1) * FROM tb_TaskInfo WHERE taskStatus IN (2) ORDER BY p_id DESC";
            SqlDataReader Reader = Comm.ExecuteReader();

            workingTaskId = "None";
            string strRecord;
            while (Reader.Read())
            {
                strRecord = string.Format("INSERT INTO tbSingle_AlarmOccurred (created_at, taskInfo_id, taskType_id, task_num, subTask_num, amr_id, alarmType_id, alarmCode) " +
                    "VALUES (GetDate(), '{0}', {1}, '{2}', {3}, {4}, {5}, {6})", Reader[1], Reader[2], Reader[3], Reader[4], Reader[5], (errorCode/50)-1, errorCode);
                listView1.Items.Add("[" + DateTimeOffset.Now.ToString() + "] " + string.Format("Write [tbSingle_AlarmOccurred] taskInfo_id = {0}, alarmType_id = {1}", Reader[1], errorCode));
                Comm.CommandText = strRecord;
                //.CommandText = strRecord;

                workingTaskId = Reader[1].ToString();
            }
            Reader.Close();
            Comm.ExecuteNonQuery();
            //Comm2.ExecuteNonQuery();
            if (workingTaskId.Equals("None"))
                listView1.Items.Add("[" + DateTimeOffset.Now.ToString() + "] Can't find the running task causing the warning "+ errorCode );

            //conn2.Close();
            conn.Close();

            if (errorCode > 750 && errorCode <= 761)
                textBox3.Text = "[" + System.Configuration.ConfigurationManager.AppSettings["AmrName"] + "] " + productLoadMsg[errorCode - 750];
            else if (errorCode > 700 && errorCode <= 712)
                textBox3.Text = "[" + System.Configuration.ConfigurationManager.AppSettings["AmrName"] + "] " + productUnloadMsg[errorCode - 700];
            else if (errorCode > 650 && errorCode <= 661)
                textBox3.Text = "[" + System.Configuration.ConfigurationManager.AppSettings["AmrName"] + "] " + spoolLoadMsg[errorCode - 650];
            else if (errorCode > 600 && errorCode <= 611)
                textBox3.Text = "[" + System.Configuration.ConfigurationManager.AppSettings["AmrName"] + "] " + spoolUnloadMsg[errorCode - 600];
            else textBox3.Text = "Unknown error code = " + errorCode;
        }

        private string CancelTask(string taskId)
        {
            conn.Close();
            conn.Open();
            SqlCommand Comm = new SqlCommand();
            Comm.Connection = conn;

            // get running task with same task info ID
            Comm.CommandText = "SELECT TOP (1) * FROM tb_TaskInfo WHERE taskInfo_id = '" + taskId + "' AND taskStatus = 2 ORDER BY p_id DESC";
            SqlDataReader Reader = Comm.ExecuteReader();

            string strRecord = "None" ;
            while (Reader.Read())
            {
                strRecord = string.Format("INSERT INTO tbSingle_TaskIssued (created_at, taskInfo_id, taskType_id, task_num, subTask_num, amr_id, cancel_yn) " +
                    "VALUES (GetDate(), '{0}', {1}, '{2}', {3}, {4}, 'Y')", Reader[1], Reader[2], Reader[3], Reader[4], Reader[5]);
                listView1.Items.Add("[" + DateTimeOffset.Now.ToString() + "] " + string.Format("Write [tbSingle_TaskIssued] taskInfo_id = {0}, Cancel task", Reader[1]));

                Comm.CommandText = strRecord;
                strRecord = string.Format("SELECT * FROM tb_TaskInfo WHERE taskInfo_id = '{0}' AND taskStatus = 4", Reader[1]);
                textBox3.Text = "[" + System.Configuration.ConfigurationManager.AppSettings["AmrName"] + "] " + string.Format("Canceling task ID = {0}", Reader[1]);
            }

            Reader.Close();
            Comm.ExecuteNonQuery();

            if (strRecord.Equals("None"))
                listView1.Items.Add("[" + DateTimeOffset.Now.ToString() + "] Can't find the running task with above taskInfo_id...");
            conn.Close();

            return strRecord;
        }

        private void TaskCanceled(string sqlQuery)
        {
            if (sqlQuery.Equals("None")) return;

            conn.Open();
            SqlCommand Comm = new SqlCommand();
            Comm.Connection = conn;

            string strRecord = "None2";
            for (int i = 0;i < 10; i++)
            {
                Comm.CommandText = sqlQuery;
                SqlDataReader Reader = Comm.ExecuteReader();

                while (Reader.Read())
                {
                    strRecord = string.Format("INSERT INTO tbSingle_TaskCanceled (created_at, taskInfo_id, task_num, subTask_num) " +
                        "VALUES (GetDate(), '{0}', {1}, '{2}')", Reader[1], Reader[3], Reader[4]);
                    listView1.Items.Add("[" + DateTimeOffset.Now.ToString() + "] " + string.Format("Write [tbSingle_TaskCanceled] taskInfo_id = {0}", Reader[1]));
                    Comm.CommandText = strRecord;
                    textBox3.Text = "[" + System.Configuration.ConfigurationManager.AppSettings["AmrName"] + "] " + string.Format("Task ID = {0} is canceled", Reader[1]);
                }
                Reader.Close();

                if (!strRecord.Equals("None2")) { Comm.ExecuteNonQuery(); break; };
                Thread.Sleep(1000);
            }

            if (strRecord.Equals("None2")) 
                listView1.Items.Add("[" + DateTimeOffset.Now.ToString() + "] Can't find the task canceled with above taskInfo_id...");

            //Comm.ExecuteNonQuery();
            conn.Close();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            //listView1.Items.Add(DateTimeOffset.Now.ToString());
            checkBox12.Checked = !checkBox12.Checked;

            timer1.Stop();

            try
            {
                if (modbusClient.Connected != true)
                    modbusClient.Connect();

                checkBox13.Checked = !checkBox13.Checked;

                isGoing = ((modbusClient.ReadInputRegisters(20, 1))[0] == 2);

                onlineStatus = modbusClient.ReadDiscreteInputs(onlineAddr, 3);
                checkBox14.Checked = !checkBox14.Checked;
                if (!Enumerable.SequenceEqual(onlineStatus, oldOnline))
                {
                    checkBox9.Checked = onlineStatus[0];
                    checkBox10.Checked = onlineStatus[1];
                    checkBox11.Checked = onlineStatus[2];

                    onlineStatus.CopyTo(oldOnline, 0);
                }

                blockedStatus = modbusClient.ReadDiscreteInputs(blkAddr, 8);
                checkBox15.Checked = !checkBox15.Checked;

                if ((blockedStatus[1] || blockedStatus[2] || blockedStatus[3] || blockedStatus[4]) && !isGoing)
                {
                    blockedStatus[1] = false; blockedStatus[2] = false; blockedStatus[3] = false; blockedStatus[4] = false;
                }

                if (!(blockedStatus.SequenceEqual(oldBlocked)) || (isGoing != isGoingPrev))
                {
                    checkBox1.Checked = blockedStatus[1];   // pause front
                    checkBox2.Checked = blockedStatus[2];   // pause rear
                    checkBox3.Checked = blockedStatus[3];   // pause front IR
                    checkBox4.Checked = blockedStatus[4];   // pause rear IR
                    checkBox5.Checked = blockedStatus[5];   // stop by bumper touch
                    checkBox6.Checked = blockedStatus[6];   // stop by emergency button press
                    checkBox7.Checked = blockedStatus[0];   // Stop by front protection
                    checkBox8.Checked = blockedStatus[7];   // loaded

                    if(dataBase)
                        AddBlockedStatus(blockedStatus);

                    if (blockedStatus[0] || blockedStatus[5] || blockedStatus[6])
                    {
                        pictureBox2.Image = Properties.Resources.stop2;
                    }
                    else if (blockedStatus[1] || blockedStatus[2] || blockedStatus[3] || blockedStatus[4])
                    {
                        pictureBox2.Image = Properties.Resources.wait2;
                    }
                    else
                    {
                        if(isGoing)
                            pictureBox2.Image = Properties.Resources.go2;
                        else
                            pictureBox2.Image = Properties.Resources.pause;
                    }

                    isGoingPrev = isGoing;
                    blockedStatus.CopyTo(oldBlocked, 0);
                }
            }
            catch (SocketException se)
            {
                pictureBox2.Image = Properties.Resources.conn2;
                //MessageBox.Show("[1]SocketException: "+se.Message);
                modbusClient = new ModbusClient(System.Configuration.ConfigurationManager.AppSettings["ModbusServer"], 502);
                oldBlocked = Enumerable.Repeat(true, 8).ToArray();
            }
            catch(IOException ex)
            {
                pictureBox2.Image = Properties.Resources.conn2;
                //MessageBox.Show("[2]IOException: " + ex.Message);
                modbusClient = new ModbusClient(System.Configuration.ConfigurationManager.AppSettings["ModbusServer"], 502);
                oldBlocked = Enumerable.Repeat(true, 8).ToArray();
            }
            catch (Exception ex)
            {
                pictureBox2.Image = Properties.Resources.conn2;
                //MessageBox.Show("[3]Exception: " + ex.Message);
                oldBlocked = Enumerable.Repeat(true, 8).ToArray();
            }
            finally
            {
                //modbusClient.Disconnect();
            }

            if (modbusAddr != 0)
                try
                {
                    if (modbusClient.Connected != true)
                        modbusClient.Connect();

                    operationStatus = modbusClient.ReadInputRegisters(modbusAddr, 2);
                    if (oldStatus[0] != operationStatus[0])
                    {
                        textBox3.Clear();

                        if (operationStatus[0] == 3)
                        {
                            listView1.Items.Add("[" + DateTimeOffset.Now.ToString() + string.Format("] *** Warning {0} occurred", operationStatus[1]));
                            if(dataBase)
                                AddAlarm(operationStatus[1]);
                            pictureBox1.Image = Properties.Resources.yelloe;

                        }
                        else if (operationStatus[0] == 2)
                        {
                            //CancelTask();
                            listView1.Items.Add("[" + DateTimeOffset.Now.ToString() + string.Format("] *** Canceling TaskId = {0}", workingTaskId));
                            if (!workingTaskId.Equals("None"))
                            {
                                if(dataBase)
                                    TaskCanceled(CancelTask(workingTaskId));
                                workingTaskId = "None";
                            }
                            pictureBox1.Image = Properties.Resources.red;
                        }
                        else
                        {
                            if(oldStatus[0] == 2 || oldStatus[0] == 3)
                                listView1.Items.Add("[" + DateTimeOffset.Now.ToString() + "] *** Warning has been cleared");
                            pictureBox1.Image = Properties.Resources.green;
                        }
                        oldStatus[0] = operationStatus[0];
                    }
                    //label4.Text = "연결 됨";
                }
                catch (SocketException se)
                {
                    oldStatus[0] = 9;
                    modbusClient = new ModbusClient(System.Configuration.ConfigurationManager.AppSettings["ModbusServer"], 502);
                    pictureBox1.Image = Properties.Resources.guage;
                }
                catch (IOException ex)
                {
                    oldStatus[0] = 9;
                    modbusClient = new ModbusClient(System.Configuration.ConfigurationManager.AppSettings["ModbusServer"], 502);
                    pictureBox1.Image = Properties.Resources.guage;
                }
                catch (Exception ex)
                {
                    //label4.Text = "연결 중...";
                    oldStatus[0] = 9;
                    pictureBox1.Image = Properties.Resources.guage;
                }
                finally
                {
                    //modbusClient.Disconnect();
                }
            else
            {
                // Do not access address 0
            }

            timer1.Start();
        }
    }

}
