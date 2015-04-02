using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using System.Text.RegularExpressions;
using System.Net;
using System.Web;
using System.IO;
using System.IO.Ports;
using System.Threading;

namespace SMSChannelMonitoringSYS
{
    public partial class FrmMain : Form
    {
        SMSHandle sh;
        ZDelegateDeclare.LogMessageShow dgShowMsg;
        Thread TsendSMS;
        public FrmMain()
        {
            InitializeComponent();
            dgShowMsg = new ZDelegateDeclare.LogMessageShow(DShowMsg);
            sh = new SMSHandle(serialPort1, ShowMsg);
            
        }
        private void FrmMain_Load(object sender, EventArgs e)
        {
            foreach(string item in SerialPort.GetPortNames())
            {
                cbPorts.Items.Add(item);
            }
            cbPorts.SelectedIndex = 0;
            XmlNodeList nodes = LoadXml();
            int row =(nodes.Count-1) / 5+1;
            for (int i = 0; i < nodes.Count; i++)
            {
                CheckBox checkbox = new CheckBox();
                checkbox.AutoSize = true;
                checkbox.Location = new System.Drawing.Point(90 * (i % 5) + 20, (i / 5) * (groupBox1.Height-40)/row + 40);
                checkbox.Name = "checkBox" + i;
                checkbox.Size = new System.Drawing.Size(90, 16);
                checkbox.TabIndex = i;
                checkbox.Tag = nodes[i].ChildNodes[2].InnerText + "&" + nodes[i].ChildNodes[3].InnerText + "&" + nodes[i].ChildNodes[4].InnerText;
                checkbox.Text = nodes[i].ChildNodes[1].InnerText;
                checkbox.UseVisualStyleBackColor = true;
                this.groupBox1.Controls.Add(checkbox);
            }
        }
        private void btnStart_Click(object sender, EventArgs e)
        {
            var controls =this.groupBox1.Controls;
            ZGlobal.ChannelList.Clear();
            foreach (CheckBox item in controls)
            {
                if(item.Checked)
                {
                    ChannelInfo  model = new ChannelInfo();
                    var temp = item.Tag.ToString().Split('&');
                    model.ChannelName = item.Text;
                    model.SendUser=temp[0];
                    model.SendUserPwd = temp[1];
                    model.ChannelUrl = temp[2];
                    lock (ZGlobal.g_oSmsQueueLock)
                    {
                        ZGlobal.ChannelList.Add(model);
                    }
                }
            }
            if (ZGlobal.ChannelList.Count< 1)
            {
                MessageBox.Show("请选择要监控的通道");
                return;
            }
            try
            {
                serialPort1.PortName = cbPorts.SelectedItem.ToString();
                if (!serialPort1.IsOpen) serialPort1.Open();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return;
            }
            lock (ZGlobal.g_oSmsQueueLock)
            {
                ZGlobal.SendAtList.Add("AT+CMGD=1,4\r");
                ZGlobal.SendAtList.Add("AT+CMGF=0\r");
            }
            btnStart.Enabled = false;
            btnStop.Enabled = true;
            sh.SetIsSendSms = false;
            sh.SetIsSend = false;
            sh.SetIsConnect = false;
            Thread Tsend = new Thread(new ThreadStart(sh.Send));
            Tsend.IsBackground = true;
            Tsend.Start();
            Thread TMonitoring = new Thread(new ThreadStart(sh.SMSMonitoring));
            TMonitoring.IsBackground = true;
            TMonitoring.Start();
            TsendSMS = new Thread(new ThreadStart(sh.SendSMS));
            TsendSMS.IsBackground = true;
            TsendSMS.Start();
        }
        private void DShowMsg(string str)
        {
            if (txtMessage.Lines.Count() > 1000)
            {
                SaveLog(txtMessage.Text);
                txtMessage.Clear();
            }
            txtMessage.AppendText(str + "\r\n");
            txtMessage.ScrollToCaret();
        }
        private void ShowMsg(string msgStr)
        {
            if (txtMessage.InvokeRequired)
            {
                txtMessage.Invoke(dgShowMsg, new object[] { msgStr });
            }
            else
            {
                if (txtMessage.Lines.Count() > 1000)
                {
                    SaveLog(txtMessage.Text);
                    txtMessage.Clear();
                }
                txtMessage.AppendText(msgStr + "\r\n");
                txtMessage.ScrollToCaret();
            }
        }
        private void btnStop_Click(object sender, EventArgs e)
        {
            btnStart.Enabled = true;
            btnStop.Enabled = false;
            TsendSMS.Abort();
            sh.SetIsSendSms = true;
            sh.SetIsSend = true;
            sh.SetIsConnect = true;
            serialPort1.Close();
            ShowMsg("程序已停止监控，停止时间："+DateTime.Now);
        }

        private void FrmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (txtMessage.Text!="")
            {
                SaveLog(txtMessage.Text);
                txtMessage.Clear();
            }
        }
        //保存日志
        private void SaveLog(string str)
        {
            string strfile = DateTime.Now.ToString("yyyyMMddHHmmss") + ".log";
            string strpath = Application.StartupPath + "\\log\\";
            if (!Directory.Exists(strpath))
            {
                Directory.CreateDirectory(strpath);
            }
            StreamWriter sw = new StreamWriter(strpath + strfile, true, Encoding.UTF8);
            sw.Write(str);
            sw.Close();
        }
        //加载通道
        private XmlNodeList LoadXml()
        {
            XmlDocument xml = new XmlDocument();
            string path = Application.StartupPath + "\\Channel.xml";
            if (System.IO.File.Exists(path))
                xml.Load(path);
            return xml.DocumentElement.ChildNodes;
        }
    }
}
