using System;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.Threading.Tasks;

namespace LaserMeasuring
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }
        UInt16 sensorA = 1; // sensorA 的设备号
        UInt16 sensorB = 2; // sensorB 的设备号

        UInt16 pointNumA = 1; // 点位标记
        UInt16 pointNumB = 1; // 点位标记

        float offsetSensorA = 0.00F; // A补偿值
        float offsetSensorB = 0.00F; // B补偿值

        MeasurePoint mp1, mp2, mp3, mp4, mp5, mp6, mp7, mp8; // 测量点

        MeasurePoint[] measurePoints = new MeasurePoint[8]; // 测量点的集合

        //获取当前时间
        private System.DateTime Current_time;
        // 全局缓存区（线程安全）
        private MemoryStream _cache = new MemoryStream(1024);

        // 窗口生成时，需要做的事情，变量初始化
        private void Form1_Load(object sender, EventArgs e)
        {
            btn_reloadPort_Click(null, null);
            LockSetting("Lock");

            float abDistance = 0.00f;

            if (!float.TryParse(txtBox_ABdistance.Text, out abDistance))
            {
                Console.WriteLine("传感器间距设置错误");
            }

            mp1 = new MeasurePoint("mp1", abDistance);
            mp2 = new MeasurePoint("mp2", abDistance);
            mp3 = new MeasurePoint("mp3", abDistance);
            mp4 = new MeasurePoint("mp4", abDistance);
            mp5 = new MeasurePoint("mp5", abDistance);
            mp6 = new MeasurePoint("mp6", abDistance);
            mp7 = new MeasurePoint("mp7", abDistance);
            mp8 = new MeasurePoint("mp8", abDistance);

            measurePoints[0] = mp1;
            measurePoints[1] = mp2;
            measurePoints[2] = mp3;
            measurePoints[3] = mp4;
            measurePoints[4] = mp5;
            measurePoints[5] = mp6;
            measurePoints[6] = mp7;
            measurePoints[7] = mp8;

            Console.WriteLine($"初始化完成，生成测量点：{mp1.pointName}, {mp2.pointName}, {mp3.pointName}, {mp4.pointName}, {mp5.pointName}, {mp6.pointName}, {mp7.pointName}, {mp8.pointName}");
        }


        #region 串口相关功能
        // 串口刷新端口号
        private void btn_reloadPort_Click(object sender, EventArgs e)
        {
            string[] comPort = System.IO.Ports.SerialPort.GetPortNames();
 
            cobBox_SeriPortNum.Items.Clear();
            cobBox_SeriPortNum.Items.AddRange(comPort);
        }

        // 串口打开、关闭串口
        private void btn_openSerial_Click(object sender, EventArgs e)
        {
            try
            {
                if (!serialPort1.IsOpen)
                {
                    serialPort1.PortName = cobBox_SeriPortNum.Text;
                    serialPort1.BaudRate = Convert.ToInt32(cobBox_BaudRate.Text);
                    serialPort1.Open();
                    btn_openSerial.Text = "关闭串口";
                    serialPort_label.Text = "串口已打开";
                    serialPort_label.BackColor = System.Drawing.Color.Green;
                    cobBox_SeriPortNum.Enabled = false;
                    cobBox_BaudRate.Enabled = false;
                }
                else
                {
                    serialPort1.Close();
                    btn_openSerial.Text = "打开串口";
                    serialPort_label.Text = "串口已关闭";
                    serialPort_label.BackColor = System.Drawing.SystemColors.ControlText;
                    cobBox_SeriPortNum.Enabled = true;
                    cobBox_BaudRate.Enabled = true;
                }
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show("对端口的访问被拒绝，端口已被其它进程占用");
            }
            catch (ArgumentOutOfRangeException)
            {
                MessageBox.Show("端口参数设置无效");
            }
            catch (ArgumentException)
            {
                MessageBox.Show("端口名出错");
            }
            catch (IOException)
            {
                MessageBox.Show("此端口处于无效状态");
            }
            catch (InvalidOperationException)
            {
                MessageBox.Show("端口已被打开");
            }
            catch (FormatException)
            {
                MessageBox.Show("波特率未设置");
            }
        }

        // 串口发送数据
        private void serialWriteHEX(string dataToSend)
        {
            byte[] hexOutput;
            hexOutput = ModbusCmd.HexStringToByteArray(dataToSend);

            try
            {
                while ((serialPort1.BytesToRead > 0) || (serialPort1.BytesToWrite > 0)) // 串口繁忙
                {
                    Console.WriteLine("串口繁忙");
                    Thread.Sleep(100);
                }

                serialPort1.Write(hexOutput, 0, hexOutput.Length);

                // 给发送信息串每两个字符加一个空格，便于信息观看
                StringBuilder dataSb = new StringBuilder();
                for (int i = 0; i < dataToSend.Length; i++)
                {
                    dataSb.Append(dataToSend[i]);
                    if ((i + 1) % 2 == 0 && i != dataToSend.Length - 1)
                    {
                        dataSb.Append(" ");
                    }
                }
                //dataSb.ToString();
                Console.WriteLine($"已发送数据: {dataSb}");

                Current_time = System.DateTime.Now;
                SetMsg("[" + Current_time.ToString("yyyy-MM-dd HH:mm:ss") + "] Send:--> " + dataSb + "\r\n");
            }
            catch (Exception)
            {
                Console.WriteLine($"串口{serialPort1.PortName}发送失败: {dataToSend}");
            }
        }

        // 串口读取数据
        private byte[] serialReadHEX()
        {
            Thread.Sleep(2); // 确保数据全部传完
            StringBuilder responseSB = new StringBuilder();

            byte[] response = new byte[serialPort1.BytesToRead];
            int read = serialPort1.Read(response, 0, response.Length);

            Console.WriteLine("收到从站回应：");
            foreach (byte b in response)
            {
                // 将数组转换成字符串
                responseSB.Append(b.ToString("X2"));
                responseSB.Append(" ");
                Console.Write($"{b:X2} ");
            }
            Console.WriteLine();
            string responseStr = responseSB.ToString();

            Current_time = System.DateTime.Now;
            SetMsg("[" + Current_time.ToString("yyyy-MM-dd HH:mm:ss") + "] Recieved:<-- " + responseSB + "\r\n");

            return response;
        }

        // 串口中断事件：当有数据收到时执行。将收到的数据按ASCII转换显示
        private void SerialPort1_DataRecived(object sender, System.IO.Ports.SerialDataReceivedEventArgs e)
        {
            try
            {
                byte[] response = serialReadHEX();

                // 使用结构体类型，将一次modbus通信的数据打包起来
                ModbusCmd.ModbusMsg_struct modbusMsg_Struct = new ModbusCmd.ModbusMsg_struct();
                modbusMsg_Struct.response = response;
                modbusMsg_Struct.idCode = response[0];
                modbusMsg_Struct.funcCode = response[1];

                // 跨线程修改UI，使用methodinvoker工具类
                MethodInvoker mi = new MethodInvoker(() =>
                {
                    Corefunc01(modbusMsg_Struct);
                });
                BeginInvoke(mi);

            }
            catch (Exception ex)
            {
                if (!this.lockSettings_checkBox.Checked)
                {
                    MessageBox.Show(ex.Message);
                }
                //seriStatus = STATUS_WAIT;
                return;
            }
        }

        #endregion

        // 处理串口数据
        private void Corefunc01(ModbusCmd.ModbusMsg_struct mbStruct)
        {
            if (mbStruct.funcCode == 4)   // 功能码为04时，返回的才是测量值
            {
                try
                {
                    mbStruct.valueFloat = ModbusCmd.CalcuDecValue(mbStruct.response);

                    if (mbStruct.idCode == sensorA)
                    {
                        // 将传感器值减去补偿值
                        offsetSensorA = float.Parse(txtBox_OffsetSensorA.Text);
                        mbStruct.valueFloat -= offsetSensorA;

                        // 给mp对象赋值
                        measurePoints[pointNumA-1].aValue = mbStruct.valueFloat;

                        mp1.aValue = mbStruct.valueFloat;

                        // 测量数据显示到UI
                        RefreshUI(mbStruct.idCode, pointNumA, mbStruct.valueFloat);

                        if (pointNumA < 8)
                        {
                            pointNumA += 1;
                        }
                        else
                            pointNumA = 1;
                    }
                    else if (mbStruct.idCode == sensorB)
                    {
                        // 将传感器值减去补偿值
                        offsetSensorB = float.Parse(txtBox_OffsetSensorB.Text);
                        mbStruct.valueFloat -= offsetSensorB;

                        // 给mp对象赋值
                        measurePoints[pointNumB-1].bValue = mbStruct.valueFloat;

                        // 测量数据显示到UI
                        RefreshUI(mbStruct.idCode, pointNumB, mbStruct.valueFloat);

                        if (pointNumB < 8)
                        {
                            pointNumB += 1;
                        }
                        else
                            pointNumB = 1;
                    }
                }
                catch (Exception)
                {
                    ;
                }
            }
        }

        // 测量数据显示到UI
        private void RefreshUI(UInt16 sensor,int pointNum,float valueFloat)
        {
            string value_str = valueFloat.ToString("0.00");// 转换为两位小数
            if (sensor == sensorA)
            {
                switch (pointNum)
                {
                    case 1:
                        txtBox_distanceA1.Text = value_str;
                        break;
                    case 2:
                        txtBox_distanceA2.Text = value_str;
                        break;
                    case 3:
                        txtBox_distanceA3.Text = value_str;
                        break;
                    case 4:
                        txtBox_distanceA4.Text = value_str;
                        break;
                    case 5:
                        txtBox_distanceA5.Text = value_str;
                        break;
                    case 6:
                        txtBox_distanceA6.Text = value_str;
                        break;
                    case 7:
                        txtBox_distanceA7.Text = value_str;
                        break;
                    case 8:
                        txtBox_distanceA8.Text = value_str;
                        break;
                    default:
                        break;
            }
            }
            else if (sensor == sensorB)
            {
                switch (pointNum)
                {
                    case 1:
                        txtBox_distanceB1.Text = value_str;
                        txtBox_Thickness1.Text = measurePoints[pointNum-1].pointThickness.ToString("F2");
                        break;
                    case 2:
                        txtBox_distanceB2.Text = value_str;
                        txtBox_Thickness2.Text = measurePoints[pointNum-1].pointThickness.ToString("F2");
                        break;
                    case 3:
                        txtBox_distanceB3.Text = value_str;
                        txtBox_Thickness3.Text = measurePoints[pointNum-1].pointThickness.ToString("F2");
                        break;
                    case 4:
                        txtBox_distanceB4.Text = value_str;
                        txtBox_Thickness4.Text = measurePoints[pointNum-1].pointThickness.ToString("F2");
                        break;
                    case 5:
                        txtBox_distanceB5.Text = value_str;
                        txtBox_Thickness5.Text = measurePoints[pointNum-1].pointThickness.ToString("F2");
                        break;
                    case 6:
                        txtBox_distanceB6.Text = value_str;
                        txtBox_Thickness6.Text = measurePoints[pointNum-1].pointThickness.ToString("F2");
                        break;
                    case 7:
                        txtBox_distanceB7.Text = value_str;
                        txtBox_Thickness7.Text = measurePoints[pointNum-1].pointThickness.ToString("F2");
                        break;
                    case 8:
                        txtBox_distanceB8.Text = value_str;
                        txtBox_Thickness8.Text = measurePoints[pointNum-1].pointThickness.ToString("F2");
                        break;
                    default:
                        break;
                }
            }
        }

        // 输出串口通信消息
        public void SetMsg(string msg)
        {
            richTextBox1.Invoke(new Action(() => { richTextBox1.AppendText(msg); }));
        }        
        
        // 文本框自动显示到最后一行
        private void richTextBox1_TextChanged(object sender, EventArgs e)
        {
            this.richTextBox1.SelectionStart = int.MaxValue;
            this.richTextBox1.ScrollToCaret();
        }

        #region 控件触发事件

        // 锁定选框点击时触发
        private void lockSettings_checkBox_CheckedChanged(object sender, EventArgs e)
        {
            string Lock_setting_status = "Lock";
            if (lockSettings_checkBox.Checked == true)
            {
                Lock_setting_status = "Lock";
            }
            else
            {
                Lock_setting_status = "UnLock";
            }
            LockSetting(Lock_setting_status);
        }

        // 锁定设置功能函数
        private void LockSetting(string status)
        {
            if (status == "UnLock")
            {
                txtBox_distanceA1.ReadOnly   = false;
                //txtBox_threshold1.ReadOnly = false;
                //txtBox_threshold2.ReadOnly = false;
            }
            else if (status == "Lock")
            {
                txtBox_distanceA1.ReadOnly = true;
                //txtBox_threshold1.ReadOnly = true;
                //txtBox_threshold2.ReadOnly = true;
            }
        }

        // 自动读取复选框
        private void chkBox_AutoRead_CheckedChanged(object sender, EventArgs e)
        {
            if (chkBox_AutoRead.Checked)
            {
                timer1.Interval = Convert.ToInt32(textBox1.Text);
                timer1.Enabled = true;
            }
            else
            {
                timer1.Enabled = false;
            }
        }

        // 读取数据 单击触发,发送modbus请求，timer定时器触发也调用它
        private void btn_ReadA_Click(object sender, EventArgs e)
        {
            serialWriteHEX(ModbusCmd.readDistance(sensorA));
        }

        private void btn_ReadB_Click(object sender, EventArgs e)
        {
            serialWriteHEX(ModbusCmd.readDistance(sensorB));
        }
        #endregion

        #region 业务逻辑-阈值判断

        // 核心监控逻辑
        private int compareThreshold(string value, string threshold) 
        {
            double valueInt = Convert.ToDouble(value);
            double thresholdInt = Convert.ToDouble(threshold);

            if (valueInt < thresholdInt)
            {
                return -1;
            }
            else if (valueInt > thresholdInt)    
            {
                return 2;
            }
            else if (valueInt == thresholdInt)
            {
                return 1;
            }
            else
                return 0;
            
        }

        private void checkValue(string value, string threshold1, string threshold2) 
        {
            int result1 = compareThreshold(value, threshold1);
            int result2 = compareThreshold(value, threshold2);
            int result = result1 + result2;
            if (result>=4)
            {
                label_messageShow.Text = "NG:高于上限";
            }
            else if (result<0)
            {
                label_messageShow.Text = "NG:低于下限";
            }
            else if (result >= 0) 
            {
                label_messageShow.Text = "OK:在区间内";
            }
        }


        #endregion

        // 测量是否在设定阈值区间内
        private void btn_Measure_Click(object sender, EventArgs e)
        {
            string value = txtBox_distanceA1.Text;
            //string threshold1 = txtBox_threshold1.Text;
            //string threshold2 = txtBox_threshold2.Text;

            try
            {
                //checkValue(value, threshold1, threshold2);
            }
            catch (Exception)
            {
                label_messageShow.Text = "错误：测量点B或者阈值为空";
            }
        }

        // 文本框只允许输入数字、退格键和小数点
        private void txtBox_distance_KeyPress(object sender, KeyPressEventArgs e)
        {
            // 允许输入数字、退格键和小数点
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) && e.KeyChar != '.')
            {
                e.Handled = true; // 阻止输入
            }

            // 只允许输入一个小数点
            if (e.KeyChar == '.' && (sender as TextBox).Text.IndexOf('.') > -1)
            {
                e.Handled = true;
            }
        }

        private void 清除文本ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            richTextBox1.Clear();
        }

        // 读取数据
        private void ReadValue(object sender, EventArgs e) 
        {

        }

        // 辅助方法：发送单个命令并返回响应
        private async Task<string> SendCommandAsync(AsyncSerialPort port, string command)
        {
            Console.WriteLine($"开始发送命令: {command}");

            // 调用异步发送接收方法
            var response = await port.SendAndReceiveAsync(command);

            Console.WriteLine($"命令 {command} 已完成");
            return response;
        }

        // 串口异步发收，且等待接收完成才开始下一个发送
        public async Task RunAsyncExample()
        {
            var serialPort = new AsyncSerialPort("COM3", 115200);
            await serialPort.OpenAsync();

            try
            {
                // 顺序执行多个发送-接收操作
                // 虽然使用了Task.WhenAll，但由于SemaphoreSlim的限制
                // 实际会按顺序执行，而非并行执行
                var responses = await Task.WhenAll(

                    SendCommandAsync(serialPort, ModbusCmd.readDistance(sensorA)),
                    SendCommandAsync(serialPort, ModbusCmd.readDistance(sensorB))
                );

                foreach (var response in responses)
                {
                    Console.WriteLine($"响应: {response}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"错误: {ex.Message}");
            }
            finally
            {
                serialPort.Close();
            }
        }



        // 清除SensorA数据
        private void button1_Click(object sender, EventArgs e)
        {
            txtBox_distanceA1.Text = "";
            txtBox_distanceA2.Text = "";
            txtBox_distanceA3.Text = "";
            txtBox_distanceA4.Text = "";
            txtBox_distanceA5.Text = "";
            txtBox_distanceA6.Text = "";
            txtBox_distanceA7.Text = "";
            txtBox_distanceA8.Text = "";
            pointNumA = 1;
        }

        // 清除SensorB数据
        private void button2_Click(object sender, EventArgs e)
        {
            txtBox_distanceB1.Text = "";
            txtBox_distanceB2.Text = "";
            txtBox_distanceB3.Text = "";
            txtBox_distanceB4.Text = "";
            txtBox_distanceB5.Text = "";
            txtBox_distanceB6.Text = "";
            txtBox_distanceB7.Text = "";
            txtBox_distanceB8.Text = "";
            pointNumB = 1;
        }

        // 单次执行按钮
        private async void button3_Click(object sender, EventArgs e)
        {
            await RunAsyncExample();
            //RunExample();
        }
    }
}
