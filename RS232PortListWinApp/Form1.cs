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
                MessageBox.Show("Port Closed");
                return;
            }

            string selectedPort = comboBoxPorts.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(selectedPort))
            {
                MessageBox.Show("Please select a port.");
                return;
            }

            try
            {
                serialPort = new SerialPort(selectedPort, 9600, Parity.None, 8, StopBits.One);
                serialPort.DataReceived += SerialPort_DataReceived;
                serialPort.Open();

                button1.Text = "Disconnect";
                MessageBox.Show("Connected to " + selectedPort);
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
               
                string connectionString = @"Server=" + Environment.MachineName + @"\SQLEXPRESS;Database=WASH;Trusted_Connection=True;TrustServerCertificate=True;";

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
    }
}
