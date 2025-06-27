using Microsoft.Data.SqlClient;
using System;
using System.IO.Ports;
using System.Windows.Forms;

namespace RS232PortListWinApp
{
    public partial class Form1 : Form
    {
        private SerialPort serialPort;
        string factory = "";
        private DateTime lastReceivedTime = DateTime.Now;

        private string GetConnectionString()
        {
            return $@"Server={Environment.MachineName}\SQLEXPRESS;Database=WASH;Trusted_Connection=True;TrustServerCertificate=True;";
        }
        public Form1()
        {
            InitializeComponent();
            LoadSerialPorts();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            string[] lines = File.ReadAllLines("Appsetting.txt");

            factory = lines
                .FirstOrDefault(line => line.StartsWith("FACTORY="))
                ?.Split('=')[1]
                ?.Trim();


            timer1.Interval = 60000;
            timer1.Start();
            timer2.Interval = 10000;
            timer2.Start();
            timer3.Interval = 30000;
            timer3.Start();
        }



        private void LoadSerialPorts()
        {
            try
            {
                // ดึงรายชื่อ Serial Ports ทั้งหมด
                string[] ports = SerialPort.GetPortNames();

                // ล้าง combobox ก่อน (เผื่อ refresh)
                comboBoxPorts.Items.Clear();

                // เพิ่มรายการลง combobox
                comboBoxPorts.Items.AddRange(ports);

                // เลือกอันแรกอัตโนมัติถ้ามี
                if (comboBoxPorts.Items.Count > 0)
                {
                    comboBoxPorts.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("เกิดข้อผิดพลาดขณะโหลดพอร์ต: " + ex.Message);
            }
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                string incomingData = serialPort.ReadExisting();
                lastReceivedTime = DateTime.Now; // <-- เพิ่มบรรทัดนี้
                // ต้องใช้ Invoke เพราะรับข้อมูลจาก Thread อื่น
                Invoke(new Action(() =>
                {
                    richTextBox1.AppendText(incomingData);
                }));

                InsertDataToDatabase(incomingData);
            }
            catch (Exception ex)
            {
                // Logging หรือ ignore ก็ได้
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (serialPort != null && serialPort.IsOpen)
            {
                serialPort.Close();
                button1.Text = "Connect";
                //  MessageBox.Show("Port Closed");
                return;
            }

            string selectedPort = comboBoxPorts.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(selectedPort))
            {
                MessageBox.Show("Please select a port." + "\n");
                return;
            }

            try
            {
                serialPort = new SerialPort(selectedPort, 9600, Parity.None, 8, StopBits.One);
                serialPort.DataReceived += SerialPort_DataReceived;
                serialPort.Open();

                button1.Text = "Disconnect";
                //  MessageBox.Show("Connected to " + selectedPort);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error opening port: " + ex.Message);
            }
        }
        private void InsertDataToDatabase(string data)
        {
            try
            {
                // ตัวอย่าง Connection String สำหรับ LocalDB (ปรับ PCNAME ถ้าจำเป็น)

                string connectionString = GetConnectionString();
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();



                    string sql = "INSERT INTO [WASH].[dbo].[TRANSACTION] (Factory, Data, CreatedDate) VALUES (@Factory, @Data, GETDATE())";

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Factory", factory);
                        cmd.Parameters.AddWithValue("@Data", data);

                        cmd.ExecuteNonQuery();


                    }


                }

            }
            catch (Exception ex)
            {
                // Log หรือแสดงข้อความ
                Invoke(new Action(() =>
                {
                    MessageBox.Show("Insert Error: " + ex.Message);
                }));
            }
        }


        private void timer1_Tick(object sender, EventArgs e)
        {
            try
            {
                string connectionString = GetConnectionString();
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();


                    string selectSql = "SELECT TransacId, Factory, Data FROM [dbo].[TRANSACTION] WHERE SendToCloundDate IS NULL";

                    using (SqlCommand selectCmd = new SqlCommand(selectSql, conn))
                    using (SqlDataReader reader = selectCmd.ExecuteReader())
                    {
                        var items = new List<(int TransacId, string Factory, string Data)>();


                        while (reader.Read())
                        {
                            items.Add((reader.GetInt32(0), reader.GetString(1), reader.GetString(2)));
                        }

                        reader.Close();

                        foreach (var item in items)
                        {
                            bool sent = false; //SendToCloudAsync(item.Factory, item.Data);

                            if (sent)
                            {
                                string updateSql = "UPDATE [dbo].[TRANSACTION] SET SendToCloundDate = GETDATE() WHERE TransacId = @TransacId";
                                using (SqlCommand updateCmd = new SqlCommand(updateSql, conn))
                                {
                                    updateCmd.Parameters.AddWithValue("@TransacId", item.TransacId);
                                    updateCmd.ExecuteNonQuery();
                                    richTextBox1.AppendText("send cloud" + "\n");
                                }


                            }

                        }
                    }

                }
            }
            catch (Exception ex)
            {
                Invoke(new Action(() =>
                {

                }));
            }
        }

        private void timer2_Tick(object sender, EventArgs e)
        {
            string connectionString = GetConnectionString();
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                string selectSql1 = "SELECT count(*) FROM [dbo].[TRANSACTION] WHERE SendToCloundDate IS NULL";


                using (SqlCommand selectCmd = new SqlCommand(selectSql1, conn))
                {
                    int count = (int)selectCmd.ExecuteScalar(); // ดึงค่าตรง ๆ
                    label2.Text = "Pending: " + count.ToString()+ "\n";
                }
            }

        }


        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (serialPort != null && serialPort.IsOpen)
            {
                serialPort.Close();
            }
        }

        private void RestartSerialPort()
        {
            try
            {
                if (serialPort != null && serialPort.IsOpen)
                {
                    serialPort.DataReceived -= SerialPort_DataReceived;
                    serialPort.Close();
                }

                string selectedPort = comboBoxPorts.SelectedItem?.ToString();
                if (!string.IsNullOrEmpty(selectedPort))
                {
                    serialPort = new SerialPort(selectedPort, 9600, Parity.None, 8, StopBits.One);
                    serialPort.DataReceived += SerialPort_DataReceived;
                    serialPort.Open();

                    lastReceivedTime = DateTime.Now;

                    Invoke(new Action(() =>
                    {
                        richTextBox1.AppendText("[Watchdog] Port restarted successfully.\n");
                    }));
                }
            }
            catch (Exception ex)
            {
                Invoke(new Action(() =>
                {
                    richTextBox1.AppendText("[Watchdog] Error restarting port: " + ex.Message + "\n");
                }));
            }
        }

        private void timer3_Tick(object sender, EventArgs e)
        {
            TimeSpan noDataDuration = DateTime.Now - lastReceivedTime;

            if (noDataDuration.TotalSeconds > 30) // ไม่มีข้อมูลเกิน 30 วินาที
            {
                Invoke(new Action(() =>
                {
                    richTextBox1.AppendText("\n[Watchdog] No data > 30s. Reconnecting serial port...\n");
                }));

                RestartSerialPort();
            }
        }
    }
}
