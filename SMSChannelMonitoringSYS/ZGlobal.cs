using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Collections;
namespace SMSChannelMonitoringSYS
{
    //文件参数信息
    class ChannelInfo
    {
        public string ChannelName;
        public string SendUser;
        public string SendUserPwd;
        public string ChannelUrl;
        public int ErrorCount;
    }
    public class SmsData 
    {
        public DateTime SendTime;
        public string SmsContent;
        public string ChannelName;
        public int Count;
    }
    //全局对象信息
    class ZGlobal
    {
        public static List<ChannelInfo> ChannelList = new List<ChannelInfo>();
        public static List<string> SendAtList = new List<string>();
        public static object g_oSmsQueueLock = new object();
        public static List<SmsData> SendSMSQueue = new List<SmsData>();
    }
}
