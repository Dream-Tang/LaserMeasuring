using System;
using System.Text;
using System.Collections.Generic;

namespace LaserMeasuring
{
    class ModbusCmd
    {
        // 结构体定义，用来将解析后的modbus数据集合起来
        public struct ModbusMsg_struct 
        {
            public byte[] response; // 串口接收的二进制消息
            public UInt16 idCode;  // 地址码
            public UInt16 funcCode; // 功能码
            public float valueFloat; // float距离值
        };

        internal const int DISTANCE = 0;

        // 读取距离所发的modbus数据
        public static string readDistance(UInt16 id)
        {
            string crcStr = "";

            // id转换成string时前面补0
            string cmd = id.ToString("D2") + "040000000271";

            // 待CRC校验数
            byte[] cmdHex = HexStringToByteArray(cmd);
            // 计算 CRC-16 校验值
            ushort crc = ModbusCRC16.CalculateCRC16(cmdHex);
            // 输出 CRC-16 校验值（低字节在前，高字节在后）
            byte lowByte = (byte)(crc & 0xFF);
            byte highByte = (byte)(crc >> 8);
            crcStr = lowByte.ToString("X2") + highByte.ToString("X2");
            cmd = cmd + crcStr;

            return cmd;
        }

        // 十六进制string转换成二进制数组，以供串口发送
        public static byte[] HexStringToByteArray(string hex)
        {
            // 十六进制string转换成二进制数组，以供串口发送
            List<byte> combinedList = new List<byte>();
            int length = hex.Length;
            byte[] bytes1 = new byte[length / 2];
            try
            {
                for (int i = 0; i < length; i += 2)
                {
                    bytes1[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
                }
            }
            catch (Exception)
            {

                Console.WriteLine("准备串口数据时出错");
            }

            // 增加回车换行
            //byte[] bytes2 = Encoding.ASCII.GetBytes("\r\n");
            combinedList.AddRange(bytes1);
            //combinedList.AddRange(bytes2);
            byte[] bytes = combinedList.ToArray();
            return bytes;
        }

        // Modbus数据解析，从response消息中计算十进制数值（传感距离读取）
        // 将数据转换成float类型输出
        public static float CalcuDecValue(byte[] response)
        {
            // respond的4到7位为寄存器值
            byte[] valueHex = { response[3], response[4], response[5], response[6] };

            // 十六进制转换为十进制（十六进制数组，高位在前）
            int valueDec = 0;
            foreach (byte b in valueHex)
            {
                // 通过左移操作和按位或运算组合字节
                valueDec = (valueDec << 8) | b;
            }
            Console.WriteLine($"十六进制数组 0x{BitConverter.ToString(valueHex).Replace("-", "")} 转换为十进制是: {valueDec}");

            // 转换成两位小数的float值
            float valueFloat = valueDec / 100.00F;
            valueFloat = (float)Math.Round(valueFloat, 2);

            return valueFloat;
        }



    }

    class ModbusCRC16
    {
        /*
        计算原理
            Modbus CRC-16 采用多项式 （对应的十六进制为 0xA001）进行计算。计算过程本质上是对要传输的数据进行一系列的位运算，最终得到一个 16 位的校验值。
            计算步骤
            1.初始化 CRC 寄存器：将 CRC 寄存器初始化为 0xFFFF。
            2.按字节处理数据：对要计算 CRC 的每一个字节，将其与 CRC 寄存器的低 8 位进行异或操作，结果存回 CRC 寄存器。
            3.进行 8 次移位和异或操作：对 CRC 寄存器进行 8 次循环处理，每次检查寄存器的最低位，如果为 1，则将寄存器右移 1 位并与 0xA001 进行异或操作；如果为 0，则直接右移 1 位。
            4.处理完所有字节：重复步骤 2 和 3，直到处理完所有要计算 CRC 的字节。
            5.得到最终 CRC 值：处理完所有字节后，CRC 寄存器中的值即为最终的 CRC-16 校验值。
        注意事项
            在 Modbus 通信中，CRC-16 校验值通常以低字节在前、高字节在后的顺序发送。
            计算 CRC 时，应确保数据的顺序和内容与实际传输的数据一致。
        */
        public static ushort CalculateCRC16(byte[] data)
        {
            /*
             代码解释
               1. 初始化 CRC 寄存器：在 CalculateCRC16 方法中，将 crc 初始化为 0xFFFF。
               2. 按字节处理数据：使用 foreach 循环遍历输入的数据数组，将每个字节与 crc 的低 8 位进行异或操作。
               3. 进行 8 次移位和异或操作：在内部的 for 循环中，对 crc 进行 8 次处理，根据最低位的值决定是否与 0xA001 进行异或操作。
               4. 输出结果：在 Main 方法中，调用 CalculateCRC16 方法计算 CRC-16 校验值，并将结果拆分为低字节和高字节输出。
            */


            // 初始化 CRC 寄存器为 0xFFFF
            ushort crc = 0xFFFF;

            // 遍历数据的每个字节
            foreach (byte b in data)
            {
                // 将当前字节与 CRC 寄存器的低 8 位进行异或操作
                crc ^= b;

                // 进行 8 次移位和异或操作
                for (int i = 0; i < 8; i++)
                {
                    if ((crc & 0x0001) != 0)
                    {
                        // 如果最低位为 1，右移 1 位并与 0xA001 异或
                        crc = (ushort)((crc >> 1) ^ 0xA001);
                    }
                    else
                    {
                        // 如果最低位为 0，直接右移 1 位
                        crc >>= 1;
                    }
                }
            }

            return crc;
        }
    }
}
