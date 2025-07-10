using System;
using System.Linq;
using System.Data.SqlClient;
using System.Windows.Forms;
using EasyModbus;
using System.Threading;
using System.Net.Sockets;
using System.IO;

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
            "[607] 적재센서 체크 결과 스풀 이재가 제대로 되지 않았습니다.",
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
            "[659] 적재센서 체크 결과 스풀 적재가 제대로 되지 않았습니다.",
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
            "[708] 적재센서 체크 결과 제품 이재가 제대로 되지 않았습니다.",
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
            "[757] 적재센서 체크 결과 제품 적재가 제대로 되지 않았습니다.",
            "[758] 브릿지 걸림센서 체크 중에 에러가 발생했습니다.",
            "[759] 푸셔 후진 중에 에러가 발생했습니다.",
            "[760] 실린더 하강 중 에러가 발생했습니다.",
            "[761] 브릿지 상승 중에 에러가 발생했습니다."
        };

        private ModbusClient modbusClient;
        private int[] operationStatus = new int[2];
        private int[] oldStatus = new int[2] { 9, 0 };
        private bool[] blockedStatus = new bool[8];
        private bool[] ioState = new bool[27];
        private bool[] oldBlocked = new bool[8] {false, false, false, false, false, false, false, false };
        private bool[] onlineStatus = new bool[3];
        private bool[] oldOnline = new bool[3] { false, false, false };
        private bool[] rackDetected = new bool[1];

        private int[] errCodes = new int[10];
        private int[] oldErrCodes = new int[10];
        private Int32 LocateErrId;
        private Int32 NaviErrId;
        private Int32 LaserErrId;
        private Int32 ChassisErrId;
        private Int32 CmdErrId;

        private SqlConnection conn = new SqlConnection();
        private SqlConnection conn2 = new SqlConnection();
        private int modbusAddr = int.Parse(System.Configuration.ConfigurationManager.AppSettings["WorkStateAddress"]);
        private int onlineAddr = int.Parse(System.Configuration.ConfigurationManager.AppSettings["OnlineStateAddress"]);
        private int blkAddr = int.Parse(System.Configuration.ConfigurationManager.AppSettings["BlockStateAddress"]);
        private int ioStateAddr = int.Parse(System.Configuration.ConfigurationManager.AppSettings["IOStateAddress"]);
        private int amrId = int.Parse(System.Configuration.ConfigurationManager.AppSettings["AmrID"]);
        private string workingTaskId = "None";
        private bool dataBase = bool.Parse(System.Configuration.ConfigurationManager.AppSettings["DataBase"]);
        private bool dataBase2 = bool.Parse(System.Configuration.ConfigurationManager.AppSettings["DataBase2"]);
        private bool dataBase3 = bool.Parse(System.Configuration.ConfigurationManager.AppSettings["DataBase3"]);
        private bool isGoing = false;
        private bool isGoingPrev = true;
        private bool isFirst = false;
        private int tagAddr = int.Parse(System.Configuration.ConfigurationManager.AppSettings["TagAddress"]);

        private Int32 optErrId; 
        private Int32 stopZoneId;
        private Int32 bumperId;
        private Int32 emergencyId;
        private Int32 loadUnloadWarnId;

        private int[] amrCoord = new int[6];
        private char firstLetter = (System.Configuration.ConfigurationManager.AppSettings["AmrName"])[0];

        public Form1()
        {
            InitializeComponent();

            this.Text = System.Configuration.ConfigurationManager.AppSettings["AmrName"] + " Alarm & Stop Monitor";
            using (frmWaitForm frm = new frmWaitForm(WaitRun))
            {
                frm.ShowDialog(this);
            }

            modbusClient = new ModbusClient(System.Configuration.ConfigurationManager.AppSettings["ModbusServer"], 502);
            conn.ConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["connString"].ConnectionString;
            conn2.ConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["connString2"].ConnectionString;

            textBox1.Text = System.Configuration.ConfigurationManager.AppSettings["AmrName"];
            if (firstLetter == 'P' || firstLetter == 'S')   // 제품 or 스풀 AMR
                checkBox8.Text = "광통신 동작";
            WriteLog("\n[" + DateTimeOffset.Now.ToString() + "] ====== Logging Start ======");
            isFirst = true;
            timer1.Start();
            if (dataBase2)
                ClearAlarmHistoryOnPOST();  // Clear alarm history on App startup
        }

        void WaitRun()
        {
            for(int i = 0; i < 30; i++)
            {
                Thread.Sleep(1000);     // 1000ms * 30 = 30sec delay 후 Main Window 시작
            }
        }

        public static void WriteLog(string strLog)
        {
            StreamWriter log;
            FileStream fileStream = null;
            DirectoryInfo logDirInfo = null;
            FileInfo logFileInfo;

            string logFilePath = ".\\Logs\\";
            logFilePath = logFilePath + "Log-" + System.DateTime.Today.ToString("yyyy-MM-dd") + "." + "txt";
            logFileInfo = new FileInfo(logFilePath);
            logDirInfo = new DirectoryInfo(logFileInfo.DirectoryName);
            if (!logDirInfo.Exists) logDirInfo.Create();
            if (!logFileInfo.Exists)
            {
                fileStream = logFileInfo.Create();
            }
            else
            {
                fileStream = new FileStream(logFilePath, FileMode.Append);
            }
            log = new StreamWriter(fileStream);
            log.WriteLine(strLog);
            log.Close();
        }

        private void ClearAlarmHistoryOnPOST()
        {
            //conn2.Close();
            conn2.Open();
            SqlCommand Comm = new SqlCommand();
            Comm.Connection = conn2;

            string strRecord;
            // Clear Server's DB alarm history
            strRecord = string.Format("UPDATE tb_AlarmHis SET cleared_at = GetDate() WHERE name = '{0}' AND cleared_at is NULL",
                System.Configuration.ConfigurationManager.AppSettings["AmrName"]);
            Comm.CommandText = strRecord;

            Comm.ExecuteNonQuery();
            conn2.Close();
        }

        private static string MyConvertRegistersToString(int[] registers, int offset, int stringLength)
        {
            byte[] array = new byte[stringLength];
            byte[] array2 = new byte[2];
            checked
            {
                for (int i = 0; i < unchecked(stringLength / 2); i++)
                {
                    array2 = BitConverter.GetBytes(registers[offset + i]);
                    array[i * 2] = array2[1];
                    array[i * 2 + 1] = array2[0];
                }

                return System.Text.Encoding.Default.GetString(array);
            }
        }

        private void UpdateAmrStatus_Monitor(int[] amrCharger, int[] amrNavi, int[] currentPoint, int[] controlMode, int[] rfid)
        {
            int[] angVel = new int[2] { amrNavi[0], amrNavi[1] };
            int[] linVel = new int[2] { amrNavi[2], amrNavi[3] };
            int[] voltage = new int[2] { amrCharger[2], amrCharger[3] };
            int[] current = new int[2] { amrCharger[4], amrCharger[5] };
            int[] percentage = new int[2] { amrCharger[6], amrCharger[7] };

            conn2.Close();
            conn2.Open();
            SqlCommand Comm = new SqlCommand();
            Comm.Connection = conn2;

            //int size0 = rfid[0].ToString().Length-1; if (size0 > 12) size0 = 12;
            //int size1 = rfid[6].ToString().Length-1; if (size0 > 12) size1 = 12;

            string strRecord;
            strRecord = string.Format("UPDATE tb_AmrStatus SET " +
                "control_mode = {0}, navigation_state = {1}, target_point = {2}, current_point = {3}, anglar_velocity = {4}" +
                ", linear_velocity = {5}, charge_state = {6}, voltage = {7}, ampere = {8}, percentage = {9}" +
                ", rfid_0 = '{10}', rfid_1 = '{11}' WHERE amr_name = '{12}'",
                controlMode[0], amrNavi[6], amrNavi[4], currentPoint[0], ModbusClient.ConvertRegistersToFloat(angVel),
                ModbusClient.ConvertRegistersToFloat(linVel), amrCharger[0], ModbusClient.ConvertRegistersToFloat(voltage),
                ModbusClient.ConvertRegistersToFloat(current), ModbusClient.ConvertRegistersToFloat(percentage),
                MyConvertRegistersToString(rfid, 0, 12),
                MyConvertRegistersToString(rfid, 6, 12),
                System.Configuration.ConfigurationManager.AppSettings["AmrName"]);
            Comm.CommandText = strRecord;

            Comm.ExecuteNonQuery();
            conn2.Close();
        }

        private void UpdateRFID_Monitor(int[] rfid)
        {
            conn2.Close();
            conn2.Open();
            SqlCommand Comm = new SqlCommand();
            Comm.Connection = conn2;

            string strRecord;
            strRecord = string.Format("UPDATE tb_AmrStatus SET " +
                "rfid_0 = '{0}', rfid_1 = '{1}' WHERE amr_name = '{2}'",
                ModbusClient.ConvertRegistersToString(rfid, 0, 12),
                ModbusClient.ConvertRegistersToString(rfid, 6, 12),
                System.Configuration.ConfigurationManager.AppSettings["AmrName"]);
            Comm.CommandText = strRecord;

            Comm.ExecuteNonQuery();
            conn2.Close();
        }

        private void UpdateBlockedStatus_Monitor(bool[] blocked)
        {
            conn2.Close();
            conn2.Open();
            SqlCommand Comm = new SqlCommand();
            Comm.Connection = conn2;

            string strRecord;
            strRecord = string.Format("UPDATE tb_BlockedStatus SET " +
                "pause_front = {0}, pause_rear = {1}, pause_front_IR = {2}, pause_rear_IR = {3}, stop_bumper = {4}" +
                ", stop_emergency = {5}, stop_front = {6}, loaded = {7} WHERE amr_name = '{8}'", 
                blocked[1] ? 1 : 0, blocked[2] ? 1 : 0, blocked[3] ? 1 : 0, blocked[4] ? 1 : 0, 
                blocked[5] ? 1 : 0, blocked[6] ? 1 : 0, blocked[0] ? 1 : 0, blocked[7] ? 1 : 0,
                System.Configuration.ConfigurationManager.AppSettings["AmrName"]);
            Comm.CommandText = strRecord;

            Comm.ExecuteNonQuery();
            conn2.Close();
        }

        private void UpdateOperationStatus_Monitor(bool[] status)
        {
            conn2.Close();
            conn2.Open();
            SqlCommand Comm = new SqlCommand();
            Comm.Connection = conn2;

            string strRecord;
            strRecord = string.Format("UPDATE tb_BlockedStatus SET " +
                "fine_position = {0}, opti_comm = {1}, read_rfid = {2} WHERE amr_name = '{3}'",
                status[0] ? 1 : 0, status[1] ? 1 : 0, status[2] ? 1 : 0,
                System.Configuration.ConfigurationManager.AppSettings["AmrName"]);
            Comm.CommandText = strRecord;

            Comm.ExecuteNonQuery();
            conn2.Close();
        }
        
        private void UpdateAmrCoord_Monitor(int[] coord)
        {
            int[] amrCoordX = new int[2] { coord[0], coord[1] };
            int[] amrCoordY = new int[2] { coord[2], coord[3] };
            int[] amrCoordTheta = new int[2] { coord[4], coord[5] };

            conn2.Close();
            conn2.Open();
            SqlCommand Comm = new SqlCommand();
            Comm.Connection = conn2;

            string strRecord;
            strRecord = string.Format("UPDATE tb_BlockedStatus SET " +
                "x_pos = {0}, y_pos = {1}, theta = {2}, updated_at = GetDate() WHERE amr_name = '{3}'",
                ModbusClient.ConvertRegistersToFloat(amrCoordX),
                ModbusClient.ConvertRegistersToFloat(amrCoordY),
                ModbusClient.ConvertRegistersToFloat(amrCoordTheta),
                System.Configuration.ConfigurationManager.AppSettings["AmrName"]);
            Comm.CommandText = strRecord;

            Comm.ExecuteNonQuery();
            conn2.Close();
        }

        private Int32 AddAlarm_Monitor(int alarmType, int alarmCode)
        {
            Int32 newProdID = 0;

            conn2.Close();
            conn2.Open();

            SqlCommand Comm = new SqlCommand();
            Comm.Connection = conn2;

            string strRecord = string.Format("INSERT INTO tb_AlarmHis (created_at, name, alarm_code, alarm_type) " +
                "VALUES (GetDate(), '{0}', '{1}', {2}); SELECT CAST(scope_identity() AS int);",
                System.Configuration.ConfigurationManager.AppSettings["AmrName"], alarmCode, alarmType);

            Comm.CommandText = strRecord;

            newProdID = (Int32)Comm.ExecuteScalar();
            conn2.Close();

            return newProdID;
        }

        private void ClearAlarm_Monitor(Int32 id)
        {
            conn2.Close();
            conn2.Open();

            SqlCommand Comm = new SqlCommand();
            Comm.Connection = conn2;

            Comm.CommandText = string.Format("UPDATE tb_AlarmHis SET cleared_at = GetDate() WHERE id = {0};", id);
            Comm.ExecuteNonQuery();

            conn2.Close();
        }

        private void AddBlockedStatus_Local(bool[] blocked)
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

        private void AddIOState_Local(bool[] ioState)
        {
            conn.Close();
            conn.Open();
            SqlCommand Comm = new SqlCommand();
            Comm.Connection = conn;

            string strRecord;

            strRecord = string.Format("INSERT tb_IOState " +
                "(updated_at,   rf_trigger_run, lc1_loaded,     lc2_loaded, touched,    emergencied,    uam_ossd1," +
                " uam_ossd3,    fine_tuned,     lc_full,        cyl_upper,  cyl_lower,  brdg_upper,     brdg_lower," +
                " di_13,        ir_rx2,         red_led,        blue_led,   brdg_fw,    brdg_bw,        charge_on, " +
                " cyl_fw,       green_led,      rfid_trigger,   buzzer,     cyl_bw,     uam_in_a,       uam_in_b) " +
                "VALUES (GetDate(), {0},  {1},  {2},  {3},  {4},  {5},  {6},  {7},  {8},  {9}, {10}, {11}, {12}, " +
                "                  {13}, {14}, {15}, {16}, {17}, {18}, {19}, {20}, {21}, {22}, {23}, {24}, {25}, {26} ) ",
                ioState[0] ? 1 : 0, ioState[1] ? 1 : 0, ioState[2] ? 1 : 0, ioState[3] ? 1 : 0, ioState[4] ? 1 : 0,
                ioState[5] ? 1 : 0, ioState[6] ? 1 : 0, ioState[7] ? 1 : 0, ioState[8] ? 1 : 0, ioState[9] ? 1 : 0,
                ioState[10] ? 1 : 0, ioState[11] ? 1 : 0, ioState[12] ? 1 : 0, ioState[13] ? 1 : 0, ioState[14] ? 1 : 0,
                ioState[15] ? 1 : 0, ioState[16] ? 1 : 0, ioState[17] ? 1 : 0, ioState[18] ? 1 : 0, ioState[19] ? 1 : 0,
                ioState[20] ? 1 : 0, ioState[21] ? 1 : 0, ioState[22] ? 1 : 0, ioState[23] ? 1 : 0, ioState[24] ? 1 : 0,
                ioState[25] ? 1 : 0, ioState[26] ? 1 : 0);

            Comm.CommandText = strRecord;

            Comm.ExecuteNonQuery();
            conn.Close();
        }

        private void AddRackDetectStatus_Local(bool[] detected)
        {
            conn.Close();
            conn.Open();
            SqlCommand Comm = new SqlCommand();
            Comm.Connection = conn;

            string strRecord;

            strRecord = string.Format("INSERT INTO tb_RackSensor (created_at, rack_detected) VALUES (GetDate(), {0})", detected[0] ? 1 : 0);
            Comm.CommandText = strRecord;
            Comm.ExecuteNonQuery();

            conn.Close();
        }

        private void AddAlarm_Local(int errorType, int errorCode)
        {
            // AMR local DB tbSingle_AlarmOccurred table 에 알람 발생 등록
            // Rack/Bobbin AMR의 scrap 처리/공 bobbin 적재 시 광통신 error
            // Spool/Product AMR의 이재, 적재 시 error
            conn.Close();
            conn.Open();

            SqlCommand Comm = new SqlCommand();
            Comm.Connection = conn;

            Comm.CommandText = "SELECT TOP (1) * FROM tb_TaskInfo WHERE taskStatus IN (2) ORDER BY p_id DESC";
            SqlDataReader Reader = Comm.ExecuteReader();

            workingTaskId = "None";
            string strRecord;
            while (Reader.Read())
            {
                strRecord = string.Format("INSERT INTO tbSingle_AlarmOccurred (created_at, taskInfo_id, taskType_id, task_num, subTask_num, amr_id, alarmType_id, alarmCode) " +
                    "VALUES (GetDate(), '{0}', {1}, '{2}', {3}, {4}, {5}, {6})", Reader[1], Reader[2], Reader[3], Reader[4], Reader[5], errorType, errorCode);
                listView1.Items.Add("[" + DateTimeOffset.Now.ToString() + "] " + string.Format("Write [tbSingle_AlarmOccurred] taskInfo_id = {0}, alarmCode = {1}", Reader[1], errorCode));
                WriteLog("[" + DateTimeOffset.Now.ToString() + "] " + string.Format("Write [tbSingle_AlarmOccurred] taskInfo_id = {0}, alarmCode = {1}", Reader[1], errorCode));
                Comm.CommandText = strRecord;

                workingTaskId = Reader[1].ToString();
            }
            Reader.Close();
            Comm.ExecuteNonQuery();
            if (workingTaskId.Equals("None"))
            {
                listView1.Items.Add("[" + DateTimeOffset.Now.ToString() + "] Can't find the running task causing the warning "+ errorCode );
                WriteLog("[" + DateTimeOffset.Now.ToString() + "] Can't find the running task causing the warning " + errorCode);
            }

            conn.Close();
        }

        private string CancelTask_Local(string taskId)
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
                WriteLog("[" + DateTimeOffset.Now.ToString() + "] " + string.Format("Write [tbSingle_TaskIssued] taskInfo_id = {0}, Cancel task", Reader[1]));

                Comm.CommandText = strRecord;
                strRecord = string.Format("SELECT * FROM tb_TaskInfo WHERE taskInfo_id = '{0}' AND taskStatus = 4", Reader[1]);
                textBox3.Text = "[" + System.Configuration.ConfigurationManager.AppSettings["AmrName"] + "] " + string.Format("Canceling task ID = {0}", Reader[1]);
            }

            Reader.Close();
            Comm.ExecuteNonQuery();

            if (strRecord.Equals("None"))
            {
                listView1.Items.Add("[" + DateTimeOffset.Now.ToString() + "] Can't find the running task with above taskInfo_id...");
                WriteLog("[" + DateTimeOffset.Now.ToString() + "] Can't find the running task with above taskInfo_id...");
            }
            conn.Close();

            return strRecord;
        }

        private void TaskCanceled_Local(string sqlQuery)
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
                    WriteLog("[" + DateTimeOffset.Now.ToString() + "] " + string.Format("Write [tbSingle_TaskCanceled] taskInfo_id = {0}", Reader[1]));
                    Comm.CommandText = strRecord;
                    textBox3.Text = "[" + System.Configuration.ConfigurationManager.AppSettings["AmrName"] + "] " + string.Format("Task ID = {0} is canceled", Reader[1]);
                }
                Reader.Close();

                if (!strRecord.Equals("None2")) { Comm.ExecuteNonQuery(); break; };
                Thread.Sleep(1000);
            }

            if (strRecord.Equals("None2"))
            {
                listView1.Items.Add("[" + DateTimeOffset.Now.ToString() + "] Can't find the task canceled with above taskInfo_id...");
                WriteLog("[" + DateTimeOffset.Now.ToString() + "] Can't find the task canceled with above taskInfo_id...");
            }

            //Comm.ExecuteNonQuery();
            conn.Close();
        }

        private void ChkNewAlarm()
        {
            conn.Close();
            conn.Open();
            SqlCommand Comm = new SqlCommand();
            Comm.Connection = conn;

            // get running task with same task info ID
            Comm.CommandText = "SELECT TOP (1) p_id, created_at, alarmType_id, alarmCode, cc_flag FROM tbSingle_AlarmOccurred WHERE alarmType_id in (6, 7, 8, 9, 10) AND cc_flag is NULL";
            SqlDataReader Reader = Comm.ExecuteReader();

            Comm.CommandText = "None";
            while (Reader.Read())
            {
                conn2.Close();
                conn2.Open();
                SqlCommand Comm2 = new SqlCommand();
                Comm2.Connection = conn2;

                Comm2.CommandText = string.Format("INSERT INTO tb_AlarmHis (created_at, name, alarm_code, alarm_type) " +
                    "VALUES ('{0}', '{1}', {2}, {3})", Reader[1], System.Configuration.ConfigurationManager.AppSettings["AmrName"], Reader[2], Reader[2]);
                Comm2.ExecuteNonQuery();

                Comm.CommandText = string.Format("UPDATE tbSingle_AlarmOccurred SET cc_flag = 1 WHERE p_id = {0}", Reader[0]);
                //Comm.ExecuteNonQuery();

                WriteLog("[" + DateTimeOffset.Now.ToString() + "] " + string.Format("Copy custom alarm type [{0}] into [tb_AlarmHis]", Reader[2]));
            }
            Reader.Close();
            if(!Comm.CommandText.Equals("None"))
                Comm.ExecuteNonQuery();

            conn2.Close();
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

                if(dataBase2)
                {
                    amrCoord = modbusClient.ReadInputRegisters(23, 6);      // x_pos, y_pos, theta
                    UpdateAmrCoord_Monitor(amrCoord);

                    if(dataBase3)
                    {
                        UpdateAmrStatus_Monitor(modbusClient.ReadInputRegisters(0, 8)
                            , modbusClient.ReadInputRegisters(14, 7)
                            , modbusClient.ReadInputRegisters(33, 2)
                            , modbusClient.ReadInputRegisters(52, 1)
                            , modbusClient.ReadInputRegisters(tagAddr, 12));    // AMR status
                    }
                }

                // AMR 고유 error codes
                errCodes = modbusClient.ReadInputRegisters(53, 10);

                if(errCodes[0] != oldErrCodes[0])
                {
                    if(errCodes[0] == 0)
                    {
                        if (dataBase2) 
                            ClearAlarm_Monitor(LocateErrId);
                        WriteLog("[" + DateTimeOffset.Now.ToString() + "] *** [Clear error] Location error cleared.");
                    }
                    else
                    {
                        AddAlarm_Local(1, errCodes[0]);
                        if (dataBase2) 
                            LocateErrId = AddAlarm_Monitor(1, errCodes[0]);
                        WriteLog("[" + DateTimeOffset.Now.ToString() + "] *** [Error] Location error " + errCodes[0].ToString() + " occurred.");
                    }
                    oldErrCodes[0] = errCodes[0];
                }

                if (errCodes[2] != oldErrCodes[2])
                {
                    if (errCodes[2] == 0)
                    {
                        if (dataBase2) 
                            ClearAlarm_Monitor(NaviErrId);
                        WriteLog("[" + DateTimeOffset.Now.ToString() + "] *** [Clear error] Navigation error cleared.");
                    }
                    else
                    {
                        AddAlarm_Local(2, errCodes[2]);
                        if (dataBase2) 
                            NaviErrId = AddAlarm_Monitor(2, errCodes[2]);
                        WriteLog("[" + DateTimeOffset.Now.ToString() + "] *** [Error] Navigation error " + errCodes[2].ToString() + " occurred.");
                    }
                    oldErrCodes[2] = errCodes[2];
                }

                if (errCodes[6] != oldErrCodes[6])
                {
                    if (errCodes[6] == 0)
                    {
                        if (dataBase2) 
                            ClearAlarm_Monitor(ChassisErrId);
                        WriteLog("[" + DateTimeOffset.Now.ToString() + "] *** [Clear error] Chassis error cleared.");
                    }
                    else
                    {
                        AddAlarm_Local(3, errCodes[6]);
                        if (dataBase2) 
                            ChassisErrId = AddAlarm_Monitor(3, errCodes[6]);
                        WriteLog("[" + DateTimeOffset.Now.ToString() + "] *** [Error] Chassis error " + errCodes[6].ToString() + " occurred.");
                    }
                    oldErrCodes[6] = errCodes[6];
                }

                if (errCodes[8] != oldErrCodes[8])
                {
                    if (errCodes[8] == 0)
                    {
                        if (dataBase2) 
                            ClearAlarm_Monitor(CmdErrId);
                        WriteLog("[" + DateTimeOffset.Now.ToString() + "] *** [Clear error] Command error cleared.");
                    }
                    else
                    {
                        AddAlarm_Local(4, errCodes[8]);
                        if (dataBase2) 
                            CmdErrId = AddAlarm_Monitor(4, errCodes[8]);
                        WriteLog("[" + DateTimeOffset.Now.ToString() + "] *** [Error] Command error " + errCodes[8].ToString() + " occurred.");
                    }
                    oldErrCodes[8] = errCodes[8];
                }

                if (errCodes[4] != oldErrCodes[4])
                {
                    if (errCodes[4] == 0)
                    {
                        if (dataBase2) 
                            ClearAlarm_Monitor(LaserErrId);
                        WriteLog("[" + DateTimeOffset.Now.ToString() + "] *** [Clear error] Laser error cleared.");
                    }
                    else
                    {
                        AddAlarm_Local(5, errCodes[4]);
                        if (dataBase2) 
                            LaserErrId = AddAlarm_Monitor(5, errCodes[4]);
                        WriteLog("[" + DateTimeOffset.Now.ToString() + "] *** [Error] Laser error " + errCodes[4].ToString() + " occurred.");
                    }
                    oldErrCodes[4] = errCodes[4];
                }

                // Check newly registered alarms
                if (dataBase3)
                    ChkNewAlarm();

                // Run online commands
                onlineStatus = modbusClient.ReadDiscreteInputs(onlineAddr, 3);  // 반사판, 광통신 오류, RFID 동작
                checkBox14.Checked = !checkBox14.Checked;

                if (!Enumerable.SequenceEqual(onlineStatus, oldOnline))
                {
                    checkBox9.Checked = onlineStatus[0];    // 반사판 찾기
                    checkBox8.Checked = onlineStatus[1];    // 랙/보빈: 광통신 오류, 제품/스풀: 광통신 동작
                    checkBox11.Checked = onlineStatus[2];   // RFID 읽기

                    if (dataBase2)
                        UpdateOperationStatus_Monitor(onlineStatus);

                    if(firstLetter == 'R' || firstLetter == 'B')    // Rack or Bobbin AMR
                    {
                        if (onlineStatus[1] && !oldOnline[1])
                        {
                            listView1.Items.Add("[" + DateTimeOffset.Now.ToString() + "] *** Optical communication error occurred");
                            WriteLog("[" + DateTimeOffset.Now.ToString() + "] *** Optical communication error occurred");
                            if (firstLetter == 'R')
                            {
                                textBox3.Text = "[" + System.Configuration.ConfigurationManager.AppSettings["AmrName"] + "] [901] 광통신 오류 발생! 스크랩 처리 여부를 확인하세요.";
                                AddAlarm_Local(16, 901);
                                if (dataBase2)
                                    optErrId = AddAlarm_Monitor(16, 901);
                            }
                            else
                            {
                                textBox3.Text = "[" + System.Configuration.ConfigurationManager.AppSettings["AmrName"] + "] [902] 광통신 오류 발생! 공보빈 적재 여부를 확인하세요.";
                                AddAlarm_Local(16, 902);
                                if (dataBase2)
                                    optErrId = AddAlarm_Monitor(16, 902);
                            }
                        }
                        else if (!onlineStatus[1] && oldOnline[1])
                        {
                            textBox3.Clear();
                            listView1.Items.Add("[" + DateTimeOffset.Now.ToString() + "] *** Optical communication error cleared");
                            WriteLog("[" + DateTimeOffset.Now.ToString() + "] *** Optical communication error cleared");
                            //if (dataBase2)
                            //    ClearAlarm_Monitor(optErrId);  / 모니터 서버 프로그램에서 r/w check box로 처리
                        }
                    }

                    onlineStatus.CopyTo(oldOnline, 0);
                }

                // Rack derected sensor state
                if (firstLetter == 'R')
                {
                    rackDetected = modbusClient.ReadDiscreteInputs(onlineAddr + 3, 1);  // address = 10043 (or 10017)
                    AddRackDetectStatus_Local(rackDetected);
                }

                if (firstLetter == 'B') // BOBBIN_AMR
                {
                    ioState = modbusClient.ReadDiscreteInputs(ioStateAddr, 27); // address = 10043
                    AddIOState_Local(ioState);
                }

                // AMR block status
                blockedStatus = modbusClient.ReadDiscreteInputs(blkAddr, 8);
                checkBox15.Checked = !checkBox15.Checked;

                if ((blockedStatus[1] || blockedStatus[2] || blockedStatus[3] || blockedStatus[4]) && !isGoing)
                {
                    blockedStatus[1] = false; blockedStatus[2] = false; blockedStatus[3] = false; blockedStatus[4] = false;
                }

                if (!(blockedStatus.SequenceEqual(oldBlocked)) || (isGoing != isGoingPrev) || isFirst)
                {
                    checkBox1.Checked = blockedStatus[1];   // pause front
                    checkBox2.Checked = blockedStatus[2];   // pause rear
                    checkBox3.Checked = blockedStatus[3];   // pause front IR
                    checkBox4.Checked = blockedStatus[4];   // pause rear IR
                    checkBox5.Checked = blockedStatus[5];   // stop by bumper touch
                    checkBox6.Checked = blockedStatus[6];   // stop by emergency button press
                    checkBox7.Checked = blockedStatus[0];   // Stop by front protection
                    checkBox10.Checked = blockedStatus[7];   // loaded

                    if (dataBase2) 
                        UpdateBlockedStatus_Monitor(blockedStatus);

                    if (dataBase)
                        AddBlockedStatus_Local(blockedStatus);

                    if (blockedStatus[0] || blockedStatus[5] || blockedStatus[6])
                    {
                        pictureBox2.Image = Properties.Resources.stop2;
                        if (blockedStatus[0] && !oldBlocked[0])
                        {
                            WriteLog("[" + DateTimeOffset.Now.ToString() + "] *** [Stop] stopped by stopzone");
                            AddAlarm_Local(15, 801);
                            if (dataBase2)
                                stopZoneId = AddAlarm_Monitor(15, 801);
                        }
                        else if (!blockedStatus[0] && oldBlocked[0])
                        {
                            WriteLog("[" + DateTimeOffset.Now.ToString() + "] *** [Clear stop] Stopzone cleared");
                            if (dataBase2)
                                ClearAlarm_Monitor(stopZoneId);
                        }

                        if (blockedStatus[5] && !oldBlocked[5])
                        {
                            WriteLog("[" + DateTimeOffset.Now.ToString() + "] *** [Stop] stopped by bumper touch");
                            AddAlarm_Local(15, 802);
                            if (dataBase2)
                                bumperId = AddAlarm_Monitor(15, 802);
                        }
                        else if (!blockedStatus[5] && oldBlocked[5])
                        {
                            WriteLog("[" + DateTimeOffset.Now.ToString() + "] *** [Clear stop] Bumper touch cleared");
                            if (dataBase2)
                                ClearAlarm_Monitor(bumperId);
                        }

                        if (blockedStatus[6] && !oldBlocked[6])
                        {
                            WriteLog("[" + DateTimeOffset.Now.ToString() + "] *** [Stop] stopped by pressing emergency button");
                            AddAlarm_Local(15, 803);
                            if (dataBase2)
                                emergencyId = AddAlarm_Monitor(15, 803);
                        }
                        else if (!blockedStatus[6] && oldBlocked[6])
                        {
                            WriteLog("[" + DateTimeOffset.Now.ToString() + "] *** [Clear stop] Eemergency state cleared");
                            if (dataBase2)
                                ClearAlarm_Monitor(emergencyId);
                        }
                    }
                    else if (blockedStatus[1] || blockedStatus[2] || blockedStatus[3] || blockedStatus[4])
                    {
                        pictureBox2.Image = Properties.Resources.wait2;
               
                        // stop > stop + wait > wait 일 경우
                        if (!blockedStatus[0] && oldBlocked[0])
                        {
                            WriteLog("[" + DateTimeOffset.Now.ToString() + "] *** [Clear stop] Stopzone cleared");
                            if (dataBase2)
                                ClearAlarm_Monitor(stopZoneId);
                        }
                        if (!blockedStatus[5] && oldBlocked[5])
                        {
                            WriteLog("[" + DateTimeOffset.Now.ToString() + "] *** [Clear stop] Bumper touch cleared");
                            if (dataBase2)
                                ClearAlarm_Monitor(bumperId);
                        }
                        if (!blockedStatus[6] && oldBlocked[6])
                        {
                            WriteLog("[" + DateTimeOffset.Now.ToString() + "] *** [Clear stop] Eemergency state cleared");
                            if (dataBase2)
                                ClearAlarm_Monitor(emergencyId);
                        }
                    }
                    else
                    {
                        if (isGoing)
                            pictureBox2.Image = Properties.Resources.go2;
                        else
                            pictureBox2.Image = Properties.Resources.pause;

                        if (!blockedStatus[0] && oldBlocked[0])
                        {
                            WriteLog("[" + DateTimeOffset.Now.ToString() + "] *** [Clear stop] Stopzone cleared");
                            if (dataBase2)
                                ClearAlarm_Monitor(stopZoneId);
                        }
                        if (!blockedStatus[5] && oldBlocked[5])
                        {
                            WriteLog("[" + DateTimeOffset.Now.ToString() + "] *** [Clear stop] Bumper touch cleared");
                            if (dataBase2)
                                ClearAlarm_Monitor(bumperId);
                        }
                        if (!blockedStatus[6] && oldBlocked[6])
                        {
                            WriteLog("[" + DateTimeOffset.Now.ToString() + "] *** [Clear stop] Eemergency state cleared");
                            if (dataBase2)
                                ClearAlarm_Monitor(emergencyId);
                        }
                    }

                    isGoingPrev = isGoing;
                    blockedStatus.CopyTo(oldBlocked, 0);
                    isFirst = false;
                }
            }
            catch (SocketException se)
            {
                pictureBox2.Image = Properties.Resources.conn2;
                //MessageBox.Show("[1]SocketException: "+se.Message);
                modbusClient = new ModbusClient(System.Configuration.ConfigurationManager.AppSettings["ModbusServer"], 502);
                isFirst = true;
                //oldBlocked = Enumerable.Repeat(true, 8).ToArray();
            }
            catch(IOException ex)
            {
                pictureBox2.Image = Properties.Resources.conn2;
                //MessageBox.Show("[2]IOException: " + ex.Message);
                modbusClient = new ModbusClient(System.Configuration.ConfigurationManager.AppSettings["ModbusServer"], 502);
                isFirst = true;
                //oldBlocked = Enumerable.Repeat(true, 8).ToArray();
            }
            catch (Exception ex)
            {
                pictureBox2.Image = Properties.Resources.conn2;
                //MessageBox.Show("[3]Exception: " + ex.Message);
                isFirst = true;
                //oldBlocked = Enumerable.Repeat(true, 8).ToArray();
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
                            int alarmTypeId = 0;

                            listView1.Items.Add("[" + DateTimeOffset.Now.ToString() + string.Format("] *** Warning {0} occurred", operationStatus[1]));
                            WriteLog("[" + DateTimeOffset.Now.ToString() + string.Format("] *** Warning {0} occurred", operationStatus[1]));

                            if (operationStatus[1] > 750 && operationStatus[1] <= 761)
                            {
                                textBox3.Text = "[" + System.Configuration.ConfigurationManager.AppSettings["AmrName"] + "] " + productLoadMsg[operationStatus[1] - 750];
                                alarmTypeId = 14;
                            }
                            else if (operationStatus[1] > 700 && operationStatus[1] <= 712)
                            {
                                textBox3.Text = "[" + System.Configuration.ConfigurationManager.AppSettings["AmrName"] + "] " + productUnloadMsg[operationStatus[1] - 700];
                                alarmTypeId = 13;
                            }
                            else if (operationStatus[1] > 650 && operationStatus[1] <= 661)
                            {
                                textBox3.Text = "[" + System.Configuration.ConfigurationManager.AppSettings["AmrName"] + "] " + spoolLoadMsg[operationStatus[1] - 650];
                                alarmTypeId = 12;
                            }
                            else if (operationStatus[1] > 600 && operationStatus[1] <= 611)
                            {
                                textBox3.Text = "[" + System.Configuration.ConfigurationManager.AppSettings["AmrName"] + "] " + spoolUnloadMsg[operationStatus[1] - 600];
                                alarmTypeId = 11;
                            }
                            else textBox3.Text = "Unknown error code = " + operationStatus[1];

                            if (dataBase)
                            {
                                AddAlarm_Local((operationStatus[1]/50)-1, operationStatus[1]);
                            }
                            if (dataBase2)
                            {
                                loadUnloadWarnId = AddAlarm_Monitor(alarmTypeId, operationStatus[1]);
                            }
                            pictureBox1.Image = Properties.Resources.yelloe;
                        }
                        else if (operationStatus[0] == 2)
                        {
                            //CancelTask();
                            listView1.Items.Add("[" + DateTimeOffset.Now.ToString() + string.Format("] *** Canceling TaskId = {0}", workingTaskId));
                            WriteLog("[" + DateTimeOffset.Now.ToString() + string.Format("] *** Canceling TaskId = {0}", workingTaskId));
                            if(dataBase2)
                                ClearAlarm_Monitor(loadUnloadWarnId);
                            if (!workingTaskId.Equals("None"))
                            {
                                if(dataBase)
                                {
                                    TaskCanceled_Local(CancelTask_Local(workingTaskId));
                                }
                                workingTaskId = "None";
                            }
                            pictureBox1.Image = Properties.Resources.red;
                        }
                        else
                        {
                            if(oldStatus[0] == 2 || oldStatus[0] == 3)
                            {
                                listView1.Items.Add("[" + DateTimeOffset.Now.ToString() + "] *** Load/Unload Warning has been cleared");
                                WriteLog("[" + DateTimeOffset.Now.ToString() + "] *** Load/Unload Warning has been cleared");
                                if (dataBase2)
                                    ClearAlarm_Monitor(loadUnloadWarnId);
                            }
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
