using System;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using System.Text;

namespace LaserMeasuring
{
    public class AsyncSerialPort:SerialPort
    {
        private readonly SerialPort _serialPort;
        // 信号量，用于限制同时只能有一个发送-接收操作在执行
        // 初始计数和最大计数都设为1，相当于互斥锁
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1); // 限制并发为1
        private readonly CancellationTokenSource _cts = new CancellationTokenSource(); // 全局取消令牌源，用于取消所有操作
        //private byte[] _response;
        private string _receivedData;
        //获取当前时间
        private System.DateTime Current_time;

        // 构造函数
        public AsyncSerialPort(string portName, int baudRate)
        {
            _serialPort = new SerialPort(portName, baudRate);
            // 注册数据接收事件处理函数
            // 当串口接收到数据时，此事件会被触发
            _serialPort.DataReceived += SerialPort_DataReceived;
        }

        public byte[] response { get; set; }

        // 异步打开串口
        // 返回已完成的任务，因为打开操作是同步的
        public Task OpenAsync()
        {
            if (!_serialPort.IsOpen)
            {
                _serialPort.Open();
            }
            return Task.CompletedTask;
        }

        // 关闭串口并取消所有正在进行的操作
        public void CloseAsync()
        {
            _cts.Cancel();
            _serialPort.Close();
        }

        // 串口数据接收事件的处理函数
        // 注意：此函数在串口驱动的线程上执行，而非主线程
        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                byte[] response =  serialReadHEX();
                _receivedData = ByteToString(response);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"接收数据时发生错误: {ex.Message}");
                _receivedData = null; // 设置为null表示接收失败
            }
        }

        // 异步发送数据并等待回复
        public async Task<string> SendAndReceiveAsync(string dataToSend, int timeout = 3000)
        {
            // 等待前一个操作完成
            await _semaphore.WaitAsync(_cts.Token).ConfigureAwait(false);

            try
            {
                // 重置接收数据，确保获取的是本次发送的响应
                _receivedData = null;

                // 发送数据
                Console.WriteLine($"发送: {dataToSend}");
                serialWriteHEX(dataToSend);

                // 创建带超时的取消令牌
                // 如果超过指定时间仍未接收到数据，将自动取消操作
                using (var timeoutCts = new CancellationTokenSource(timeout))
                using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    _cts.Token, timeoutCts.Token))
                {
                    // 循环检查是否接收到数据，或操作是否被取消
                    while (string.IsNullOrEmpty(_receivedData))
                    {
                        // 短暂等待，避免CPU占用过高
                        // 此Delay是可取消的，当任一令牌被触发时会立即返回
                        await Task.Delay(10, linkedCts.Token).ConfigureAwait(false);
                    }
                    // 返回接收到的数据
                    
                    Current_time = System.DateTime.Now;
                    string msg = "[" + Current_time.ToString("yyyy-MM-dd HH:mm:ss") + "] Recieved:<-- " + _receivedData + "\r\n";
                    Console.Write(msg);

                    return _receivedData;
                }
            }
            catch (OperationCanceledException)
            {
                // 处理操作被取消的情况（可能是超时或手动取消）
                Console.WriteLine("操作超时或被取消");
                return null;
            }
            finally
            {
                // 释放信号量，允许下一个操作开始
                // 无论操作成功还是失败，都必须释放信号量
                _semaphore.Release();
            }
        }

        // 串口发送HEX数据
        public string serialWriteHEX(string dataToSend)
        {
            byte[] hexOutput;
            hexOutput = ModbusCmd.HexStringToByteArray(dataToSend);

            try
            {
                _serialPort.Write(hexOutput, 0, hexOutput.Length);

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

                Current_time = System.DateTime.Now;
                string msg = "[" + Current_time.ToString("yyyy-MM-dd HH:mm:ss") + "] Send:--> " + dataSb ;
                Console.Write(msg);
                return msg;
            }
            catch (Exception)
            {
                string msg = $"串口{_serialPort.PortName}发送失败: {dataToSend}";
                Console.WriteLine(msg);
                return msg;
            }
        }

        // 串口读取HEX数据
        public byte[] serialReadHEX()
        {
            Thread.Sleep(2); // 确保数据全部传完

            response = new byte[_serialPort.BytesToRead];
            int read = _serialPort.Read(response, 0, response.Length);

            _receivedData = ByteToString(response);

            Console.Write($"收到从站回应：{_receivedData}");

            return response;
        }

        // 二进制转换成string，在接收数据后使用
        public string ByteToString(byte[] response)
        {
            StringBuilder responseSB = new StringBuilder();
            
            foreach (byte b in response)
            {
                // 将数组转换成字符串
                responseSB.Append(b.ToString("X2"));
                responseSB.Append(" ");
            }
            Console.WriteLine();

            string responseStr = responseSB.ToString();
            return responseStr;
        }

    }
}
