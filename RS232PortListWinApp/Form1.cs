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
                // �֧��ª��� Serial Ports ������
                string[] ports = SerialPort.GetPortNames();

                // ��ҧ combobox ��͹ (���� refresh)
                comboBoxPorts.Items.Clear();

                // ������¡��ŧ combobox
                comboBoxPorts.Items.AddRange(ports);

                // ���͡�ѹ�á�ѵ��ѵԶ����
                if (comboBoxPorts.Items.Count > 0)
                {
                    comboBoxPorts.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("�Դ��ͼԴ��Ҵ�����Ŵ����: " + ex.Message);
            }
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                string incomingData = serialPort.ReadExisting();

                // ��ͧ�� Invoke �����Ѻ�����Ũҡ Thread ���
                Invoke(new Action(() =>
                {
                    richTextBox1.AppendText(incomingData);
                }));

                InsertDataToDatabase(incomingData);
            }
            catch (Exception ex)
            {
                // Logging ���� ignore ����
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
                // ������ҧ Connection String ����Ѻ LocalDB (��Ѻ PCNAME ��Ҩ���)
               
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
                // Log �����ʴ���ͤ���
                Invoke(new Action(() =>
                {
                    MessageBox.Show("Insert Error: " + ex.Message);
                }));
            }
        }
    }
}
