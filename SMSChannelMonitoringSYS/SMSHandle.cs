using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Xml;
using System.Windows.Forms;
using System.Threading;
using System.Web;
using System.Net;
using System.IO;
using System.IO.Ports;
using System.Configuration;

namespace SMSChannelMonitoringSYS
{
    public class SMSHandle
    {
        private ZDelegateDeclare.LogMessageShow dgShowMsg;
        private SerialPort SerialPort;
        private bool IsConnect;
        private bool IsSendSms;
        private bool IsSend;
        private string telPhone = ConfigurationManager.AppSettings["TelPhone"].ToString();
        private string Phone = ConfigurationManager.AppSettings["Phone"].ToString();
        private int TimeInterval = int.Parse(ConfigurationManager.AppSettings["TimeInterval"].ToString());
        private int SelectTime =int.Parse(ConfigurationManager.AppSettings["SelectTime"].ToString());
        private int ErrorCount = int.Parse(ConfigurationManager.AppSettings["ErrorCount"].ToString());
        private string[] SubmitErrorCount = ConfigurationManager.AppSettings["SubmitErrorCount"].ToString().Split(',');
        private string serialPortMsg = string.Empty;
        private List<string> ErrorMsg= new List<string>();

        public bool SetIsConnect
        {
            set { IsConnect = value; }
        }
        public bool SetIsSend
        {
            set { IsSend = value; }
        }
        public bool SetIsSendSms
        {
            set { IsSendSms = value; }
        }

        public SMSHandle(SerialPort SerialPort, ZDelegateDeclare.LogMessageShow dgShowMsg) 
        {
            this.SerialPort = SerialPort;
            this.dgShowMsg = dgShowMsg;
            this.SerialPort.DataReceived += new System.IO.Ports.SerialDataReceivedEventHandler(this.serialPort1_DataReceived);
        }
        //发送短信
        public void SendSMS()
        {
            dgShowMsg("开始发送测试短信。。。");
            while (!IsSendSms)
            {
                lock (ZGlobal.g_oSmsQueueLock)
                {
                    foreach (var item in ZGlobal.ChannelList)
                    {
                        string SMSContent = HttpUtility.UrlEncode("测试短信" + DateTime.Now.ToString("yyyyMMddHHmmssfff"), Encoding.GetEncoding("gbk"));
                        string[] arrValue = SubmitSms(item, SMSContent);
                        DateTime StartTime = DateTime.Now;
                        if (arrValue.Length == 2 && arrValue[0] == "0")
                        {
                            SmsData sd = new SmsData();
                            sd.SendTime = StartTime;
                            sd.ChannelName = item.ChannelName;
                            sd.SmsContent = SMSContent.Substring(SMSContent.Length - 17);
                            lock (ZGlobal.g_oSmsQueueLock)
                            {
                                ZGlobal.SendSMSQueue.Add(sd);
                                ZGlobal.SendAtList.Add("AT+CMGL=4\r");
                                item.ErrorCount = 0;
                            }
                            dgShowMsg("测试短信发送成功,时间为：" + StartTime);
                        }
                        else
                        {
                            dgShowMsg("测试短信提交失败,时间为：" + StartTime);
                            lock (ZGlobal.g_oSmsQueueLock)
                            {
                                item.ErrorCount++;
                                if (SubmitErrorCount.Contains(item.ErrorCount.ToString()))
                                {
                                    string content = GetString(item.ChannelName + "提交失败，返回值" + arrValue[0]);
                                    if (content.Length > 100) content = content.Substring(0, 100);
                                    foreach (var phone in GetPhone())
                                    {
                                        string msg = "0011000d9168" + phone + "000801" + (content.Length / 2).ToString("x") + "" + content + ((char)0X1a).ToString();
                                        ErrorMsg.Add(msg);
                                        ZGlobal.SendAtList.Add("AT+CMGS=" + ((msg.Length - 5) / 2 + 1) + "\r");
                                    }
                                }
                            }
                        }
                    }
                }
                Thread.Sleep(TimeInterval * 60 * 1000);
            }
        }
        private void serialPort1_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            dgShowMsg("接收到数据");
            string str = SerialPort.ReadExisting();
            serialPortMsg += str;
            if (str.Contains("ERROR"))
            {
                serialPortMsg = string.Empty;
                return;
            }
            if (!(str.Contains("OK") || str.Contains(">")))
                return;
            dgShowMsg(serialPortMsg);
            string[] blacklist = serialPortMsg.Replace("\r\n", "*").Split('*');
            serialPortMsg = string.Empty;
            if (blacklist[0] == "AT+CMGF=0\r" && blacklist[1] == "OK")
            {
                lock (ZGlobal.g_oSmsQueueLock)
                {
                    ZGlobal.SendAtList.Remove(blacklist[0]);
                }
            }
            if (blacklist[0] == "AT+CMGL=4\r" && blacklist[1] != "OK")
            {
                for (int i = 0; i < (blacklist.Length - 3) / 3; i++)
                {
                    int index = i * 3 + 2;
                    if (blacklist[index].Contains("3010") && blacklist[index].Contains("3011"))
                    {
                        blacklist[index] = blacklist[index].Replace("3010", "&");
                        blacklist[index] = blacklist[index].Substring(0, blacklist[index].IndexOf('&'));
                    }
                    string time = GetJieMString(blacklist[index].Substring(blacklist[index].Length - 68));
                    SmsData sms= ZGlobal.SendSMSQueue.FirstOrDefault(s=>s.SmsContent==time);
                    if (sms!=null)
                    {
                        lock (ZGlobal.g_oSmsQueueLock) 
                        {
                            ZGlobal.SendAtList.Remove(blacklist[0]);
                            ZGlobal.SendSMSQueue.Remove(sms);
                            int smsindex = int.Parse(blacklist[index - 1].Split(',')[0].Split(':')[1].TrimStart());
                            ZGlobal.SendAtList.Add("AT+CMGD=" + smsindex + "\r"); 
                        };
                    }
                }
            }
            if (blacklist[0].Contains("AT+CMGD=") && blacklist[1] == "OK")
            {
                lock (ZGlobal.g_oSmsQueueLock) { ZGlobal.SendAtList.Remove(blacklist[0]); }
                return;
            }
            if (blacklist[0].Contains("AT+CMGS=") && blacklist[1] == "> ")
            {
                string msg = ErrorMsg.FirstOrDefault(m => ((m.Length - 5) / 2 + 1).ToString()+"\r" == blacklist[0].Split('=')[1]);
                SerialPort.Write(msg + "\r");
                return;
            }
            if (blacklist[1].Contains("+CMGS:"))
            {
                ErrorMsg.Remove(blacklist[0]);
                lock (ZGlobal.g_oSmsQueueLock) 
                {
                    ZGlobal.SendAtList.Remove("AT+CMGS=" + ((blacklist[0].Length - 5) / 2 + 1) + "\r");
                }
                dgShowMsg("出现异常,已成功发出报警短信");
                return;
            }
        }
        //向串口发送消息
        public void Send()
        {
            dgShowMsg("正在发送at命令。。。");
            while (!IsSend)
            {
                try
                {
                    lock (ZGlobal.g_oSmsQueueLock)
                    {
                        foreach (string item in ZGlobal.SendAtList)
                        {
                            SerialPort.Write(item);
                            Thread.Sleep(1000);
                        }
                    }
                }
                catch (Exception e1)
                {
                    dgShowMsg("SendDataThread-e1:" + e1.Message);
                }
                Thread.Sleep(50);
            }
        }
        //监控已发送短信
        public void SMSMonitoring() 
        {
            dgShowMsg("正在检测通道状况。。。");
            while (!IsConnect)
            {
                Thread.Sleep(1000);
                lock (ZGlobal.g_oSmsQueueLock)
                {
                    for (int i = 0; i < ZGlobal.SendSMSQueue.Count; i++)
                    {
                        SmsData item = ZGlobal.SendSMSQueue[i];
                        if ((DateTime.Now - item.SendTime).TotalMinutes > SelectTime)
                        {
                            if (item.Count >= ErrorCount-1)
                            {
                                ChannelInfo ci = ZGlobal.ChannelList.FirstOrDefault(e => e.ChannelName == item.ChannelName);
                                if (ci != null) ZGlobal.ChannelList.Remove(ci);
                                string content = GetString(item.ChannelName + "出现异常");
                                foreach (var phone in GetPhone())
                                {
                                    string msg = "0011000d9168" + phone + "000801" + (content.Length / 2).ToString("x") + "" + content + ((char)0X1a).ToString();
                                    ErrorMsg.Add(msg);
                                    ZGlobal.SendAtList.Add("AT+CMGS=" + ((msg.Length - 5) / 2 + 1) + "\r");
                                }
                                ZGlobal.SendAtList.Remove("AT+CMGL=4\r");
                                ZGlobal.SendSMSQueue.Remove(item);
                                i--;
                            }
                            else 
                            {
                                string SMSContent = HttpUtility.UrlEncode("测试短信" + DateTime.Now.ToString("yyyyMMddHHmmssfff"), Encoding.GetEncoding("gbk"));
                                ChannelInfo ci = ZGlobal.ChannelList.FirstOrDefault(e => e.ChannelName == item.ChannelName);
                                string[] arrValue = SubmitSms(ci, SMSContent);
                                DateTime StartTime = DateTime.Now;
                                if (arrValue.Length == 2 && arrValue[0] == "0")
                                {
                                    item.Count++;
                                    item.SendTime = StartTime;
                                    item.SmsContent = SMSContent.Substring(SMSContent.Length - 17);
                                    dgShowMsg("测试短信发送成功,时间为：" + StartTime);
                                }
                                else { dgShowMsg("测试短信发送失败,时间为：" + StartTime); }
                            }
                        }
                    }
                }
            }
        }
        //向通道提交短信
        private string[] SubmitSms(ChannelInfo item, string SMSContent)
        {
            dgShowMsg("正在向"+item.ChannelName+"提交短信,提交时间为："+DateTime.Now);
            try
            {
                string strURL =item.ChannelUrl+"usr=" + item.SendUser + "&pwd=" + item.SendUserPwd
                            + "&mobile=" + telPhone + "&sms=" + SMSContent + ""
                            + "&extdsrcid=";
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(strURL);
                request.ContentType = "application/x-www-form-urlencoded;charset=gbk";
                request.Accept = "*/*";
                request.Method = "GET";
                request.Timeout = 30000;
                request.KeepAlive = false;
                System.Net.ServicePointManager.DefaultConnectionLimit = 250;
                request.ProtocolVersion = HttpVersion.Version11;
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                if (request.HaveResponse)
                {
                    Stream receiveStream = response.GetResponseStream();
                    StreamReader readStream = new StreamReader(receiveStream, Encoding.ASCII);
                    string strReturnValue = readStream.ReadToEnd().Trim();
                    receiveStream.Close();
                    readStream.Close();
                    response.Close();
                    string[] arrValue = strReturnValue.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    return arrValue;
                }
                else { return new string[]{"未收到资源"}; }
            }
            catch (Exception ex) { dgShowMsg(ex.Message + DateTime.Now);return new string[] { "服务器错误" }; }
        }
        //字符格式转换
        private string GetString(string msg)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char item in msg)
            {
                int value = Convert.ToInt32(item);
                if (value > 255)
                    sb.Append(value.ToString("x"));
                else
                    sb.Append("00" + value.ToString("x"));
            }
            return sb.ToString();
        }
        //字符格式转换
        private string GetJieMString(string msg)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < msg.Length / 4; i++)
            {
                int index = 4 * i;
                string temp = msg[index].ToString() + msg[index + 1] + msg[index + 2] + msg[index + 3];
                sb.Append((char)Convert.ToInt32(temp, 16));
            }
            return sb.ToString();
        }
        //报警号码转换
        private List<string> GetPhone() 
        {
            List<string> list = new List<string>();
            foreach (var item in Phone.Split(','))
            {
                if (item == "") continue;
                StringBuilder sb = new StringBuilder(12);
                string phone = item + "F";
                for (int i = 0; i < phone.Length / 2; i++)
                {
                    sb.Append(phone[2 * i + 1]);
                    sb.Append(phone[2 * i]);
                }
                list.Add(sb.ToString());
            }
            return list;
        }
    }
}

