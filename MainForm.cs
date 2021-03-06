﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace emotion_viewer.cs
{
    public partial class MainForm : Form
    {
        private bool isLearnState = false;

        private bool isValidateState = false;

        private TextBox actualTextBox;
        private List<string> emotions = new List<string>();

        public PXCMSession session;
        private volatile bool closing = false;
        public volatile bool stop = false;
        private Bitmap bitmap = null;
        private string filename = null;
        public Dictionary<string, PXCMCapture.DeviceInfo> Devices { get; set; }

        private string[] EmotionLabels = { "ANGER", "CONTEMPT", "DISGUST", "FEAR", "JOY", "SADNESS", "SURPRISE" };
        private string[] SentimentLabels = { "NEGATIVE", "POSITIVE", "NEUTRAL" };

        public int NUM_EMOTIONS = 10;
        public int NUM_PRIMARY_EMOTIONS = 7;

        public MainForm(PXCMSession session)
        {
            InitializeComponent();

            this.session = session;
            PopulateDeviceMenu();
            PopulateModuleMenu();

            FormClosing += new FormClosingEventHandler(MainForm_FormClosing);
            Panel2.Paint += new PaintEventHandler(Panel_Paint);
        }

        public void PopulateDeviceMenu()
        {
            Devices = new Dictionary<string, PXCMCapture.DeviceInfo>();

            PXCMSession.ImplDesc desc = new PXCMSession.ImplDesc();
            desc.group = PXCMSession.ImplGroup.IMPL_GROUP_SENSOR;
            desc.subgroup = PXCMSession.ImplSubgroup.IMPL_SUBGROUP_VIDEO_CAPTURE;
            ToolStripMenuItem sm = new ToolStripMenuItem("Device");
            for (int i = 0; ; i++)
            {
                PXCMSession.ImplDesc desc1;
                if (session.QueryImpl(desc, i, out desc1) < pxcmStatus.PXCM_STATUS_NO_ERROR) break;
                PXCMCapture capture;
                if (session.CreateImpl<PXCMCapture>(desc1, out capture) < pxcmStatus.PXCM_STATUS_NO_ERROR) continue;
                for (int j = 0; ; j++)
                {
                    PXCMCapture.DeviceInfo dinfo;
                    if (capture.QueryDeviceInfo(j, out dinfo) < pxcmStatus.PXCM_STATUS_NO_ERROR) break;
                    if (!Devices.ContainsKey(dinfo.name))
                        Devices.Add(dinfo.name, dinfo);
                    ToolStripMenuItem sm1 = new ToolStripMenuItem(dinfo.name, null, new EventHandler(Device_Item_Click));
                    sm.DropDownItems.Add(sm1);
                }
                capture.Dispose();
            }
            if (sm.DropDownItems.Count > 0)
                (sm.DropDownItems[0] as ToolStripMenuItem).Checked = true;
            MainMenu.Items.RemoveAt(0);
            MainMenu.Items.Insert(0, sm);
        }

        private void PopulateModuleMenu()
        {
            PXCMSession.ImplDesc desc = new PXCMSession.ImplDesc();
            desc.cuids[0] = PXCMEmotion.CUID;
            ToolStripMenuItem mm = new ToolStripMenuItem("Module");
            for (int i = 0; ; i++)
            {
                PXCMSession.ImplDesc desc1;
                if (session.QueryImpl(desc, i, out desc1) < pxcmStatus.PXCM_STATUS_NO_ERROR) break;
                ToolStripMenuItem mm1 = new ToolStripMenuItem(desc1.friendlyName, null, new EventHandler(Module_Item_Click));
                mm.DropDownItems.Add(mm1);
            }
            if (mm.DropDownItems.Count > 0)
                (mm.DropDownItems[0] as ToolStripMenuItem).Checked = true;
            try
            {
                MainMenu.Items.RemoveAt(1);
            }
            catch
            {
                mm.Dispose();
            }
            MainMenu.Items.Insert(1, mm);
        }

        private void RadioCheck(object sender, string name)
        {
            foreach (ToolStripMenuItem m in MainMenu.Items)
            {
                if (!m.Text.Equals(name)) continue;
                foreach (ToolStripMenuItem e1 in m.DropDownItems)
                {
                    e1.Checked = (sender == e1);
                }
            }
        }

        private void Device_Item_Click(object sender, EventArgs e)
        {
            RadioCheck(sender, "Device");
        }

        private void Module_Item_Click(object sender, EventArgs e)
        {
            RadioCheck(sender, "Module");
        }

        private void Profile_Item_Click(object sender, EventArgs e)
        {
            RadioCheck(sender, "Profile");
        }

        delegate void DoTrackingCompleted();
        private void DoTracking()
        {
            EmotionDetection ft = new EmotionDetection(this);
            ft.SimplePipeline();
            //this.textBox1.Lines = emotions.ToArray();

            this.Invoke(new DoTrackingCompleted(
                delegate
                {
                    MainMenu.Enabled = true;
                    if (closing) Close();
                }
            ));
        }

        public string GetCheckedDevice()
        {
            foreach (ToolStripMenuItem m in MainMenu.Items)
            {
                if (!m.Text.Equals("Device")) continue;
                foreach (ToolStripMenuItem e in m.DropDownItems)
                {
                    if (e.Checked) return e.Text;
                }
            }
            return null;
        }

        public string GetCheckedModule()
        {
            foreach (ToolStripMenuItem m in MainMenu.Items)
            {
                if (!m.Text.Equals("Module")) continue;
                foreach (ToolStripMenuItem e in m.DropDownItems)
                {
                    if (e.Checked) return e.Text;
                }
            }
            return null;
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            stop = true;
            closing = true;
        }

        private delegate void UpdateStatusDelegate(string status);
        public void UpdateStatus(string status)
        {
            Status2.Invoke(new UpdateStatusDelegate(delegate(string s) { StatusLabel.Text = s; }), new object[] { status });

            if (this.stop)
            {
                this.isLearnState = false;
                this.isValidateState = false;
                Status2.Invoke(new UpdateStatusDelegate(delegate(string s) { this.button1.Text = "Learn"; }), new object[] { status });
                Status2.Invoke(new UpdateStatusDelegate(delegate(string s) { this.button3.Text = "Enter key"; }), new object[] { status });
            }
        }

        public void DisplayBitmap(Bitmap picture)
        {
            lock (this)
            {
                bitmap = new Bitmap(picture);
            }
        }

        private void Panel_Paint(object sender, PaintEventArgs e)
        {
            lock (this)
            {
                if (bitmap == null) return;
                if (!(sender is PictureBox)) return;
                if (true)
                {
                    /* Keep the aspect ratio */
                    Rectangle rc = ((PictureBox)sender).ClientRectangle;
                    float xscale = (float)rc.Width / (float)bitmap.Width;
                    float yscale = (float)rc.Height / (float)bitmap.Height;
                    float xyscale = (xscale < yscale) ? xscale : yscale;
                    int width = (int)(bitmap.Width * xyscale);
                    int height = (int)(bitmap.Height * xyscale);
                    rc.X = (rc.Width - width) / 2;
                    rc.Y = (rc.Height - height) / 2;
                    rc.Width = width;
                    rc.Height = height;
                    e.Graphics.DrawImage(bitmap, rc);
                }
                else
                {
                    e.Graphics.DrawImageUnscaled(bitmap, 0, 0);
                }
            }
        }

        private delegate void UpdatePanelDelegate();
        public void UpdatePanel()
        {
            Panel2.Invoke(new UpdatePanelDelegate(delegate()
                {
                    Panel2.Invalidate();
                }));
        }

        private void simpleToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RadioCheck(sender, "Pipeline");
        }

        private void advancedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RadioCheck(sender, "Pipeline");
        }

        public bool GetPlaybackState()
        {
            return false;
        }

        public bool GetRecordState()
        {
            return false;
        }

        public void DrawLocation(PXCMEmotion.EmotionData[] data)
        {
            lock (this)
            {
                if (bitmap == null) return;
                using (Graphics g = Graphics.FromImage(bitmap))
                using (Pen red = new Pen(Color.Red, 3.0f))
                using (Brush brushTxt = new SolidBrush(Color.Cyan))
                using (Font font = new Font(Font.FontFamily, 11, FontStyle.Bold))
                {
                    if (true)
                    {
                        Point[] points4 =
                        {
                            new Point(data[0].rectangle.x, data[0].rectangle.y),
                            new Point(data[0].rectangle.x + data[0].rectangle.w, data[0].rectangle.y),
                            new Point(data[0].rectangle.x + data[0].rectangle.w,
                                data[0].rectangle.y + data[0].rectangle.h),
                            new Point(data[0].rectangle.x, data[0].rectangle.y + data[0].rectangle.h),
                            new Point(data[0].rectangle.x, data[0].rectangle.y)
                        };
                        g.DrawLines(red, points4);
                        //g.DrawString(data[0].fid.ToString(), font, brushTxt, (float)(data[0].rectangle.x + data[0].rectangle.w), (float)(data[0].rectangle.y));
                    }

                    bool emotionPresent = false;
                    int epidx = -1;
                    int maxscoreE = -3;
                    float maxscoreI = 0;
                    for (int i = 0; i < NUM_PRIMARY_EMOTIONS; i++)
                    {
                        if (data[i].evidence < maxscoreE) continue;
                        if (data[i].intensity < maxscoreI) continue;
                        maxscoreE = data[i].evidence;
                        maxscoreI = data[i].intensity;
                        epidx = i;
                    }
                    if ((epidx != -1) && (maxscoreI > 0.4))
                    {
                        g.DrawString(EmotionLabels[epidx], font, brushTxt, data[0].rectangle.x + data[0].rectangle.w,
                            data[0].rectangle.y > 0 ? data[0].rectangle.y : data[0].rectangle.h - 2 * font.GetHeight());
                        emotionPresent = true;
                    }

                    int spidx = -1;
                    if (emotionPresent)
                    {
                        maxscoreE = -3;
                        maxscoreI = 0;
                        for (int i = 0; i < (NUM_EMOTIONS - NUM_PRIMARY_EMOTIONS); i++)
                        {
                            if (data[NUM_PRIMARY_EMOTIONS + i].evidence < maxscoreE) continue;
                            if (data[NUM_PRIMARY_EMOTIONS + i].intensity < maxscoreI) continue;
                            maxscoreE = data[NUM_PRIMARY_EMOTIONS + i].evidence;
                            maxscoreI = data[NUM_PRIMARY_EMOTIONS + i].intensity;
                            spidx = i;
                        }
                        if ((spidx != -1))
                        {
                            g.DrawString(SentimentLabels[spidx], font, brushTxt,
                                data[0].rectangle.x + data[0].rectangle.w,
                                data[0].rectangle.y > 0
                                    ? data[0].rectangle.y + font.GetHeight()
                                    : data[0].rectangle.h - font.GetHeight());
                        }
                    }

                    if ((epidx != -1) && (maxscoreI > 0.4) && spidx != -1)
                    {
                        string emotion = EmotionLabels[epidx] + " " + SentimentLabels[spidx];

                        this.actualTextBox.Invoke(new UpdateStatusDelegate(delegate(string s)
                        {
                            var list = this.actualTextBox.Lines.ToList();
                            var lastEmotion = list.LastOrDefault();

                            if (lastEmotion != s)
                            {
                                list.Add(s);
                            }

                            if (actualTextBox == textBox2)
                            {
                                var validatedEmotions = list;
                                var keyEmotions = textBox1.Lines.ToList();

                                bool validate = true;

                                for (int i = 0; i < validatedEmotions.Count; i++)
                                {
                                    if (keyEmotions[i] != validatedEmotions[i])
                                    {
                                        validate = false;
                                        break;
                                    }
                                }

                                if (!validate)
                                {
                                    list = new List<string>();
                                }

                                if (validatedEmotions.Count == keyEmotions.Count && list.Count > 0)
                                {
                                    list.Add("Hello!");
                                    this.stop = true;
                                }
                            }

                            this.actualTextBox.Lines = list.ToArray();

                            if (list.Count > 2)
                            {
                                this.stop = true;
                            }

                        }), new object[] { emotion });
                    }
                }
            }
        }

        public string GetFileName()
        {
            return filename;
        }

        private bool DisplayDeviceConnection(bool state)
        {
            if (state)
            {
                if (!disconnected) this.UpdateStatus("Device Disconnected");
                disconnected = true;
            }
            else
            {
                if (disconnected) this.UpdateStatus("Device Reconnected");
                disconnected = false;
            }
            return disconnected;
        }

        private bool disconnected = false;
        private FPSTimer timer;

        private void MainForm_Load(object sender, EventArgs e)
        {

        }

        System.Threading.Thread thread;

        private void HandleEmotions(TextBox box)
        {
            this.actualTextBox = box;
            stop = false;
            System.Threading.Thread thread = new System.Threading.Thread(DoTracking);
            thread.Start();
            System.Threading.Thread.Sleep(5);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            this.isLearnState = !this.isLearnState;
            if (this.isLearnState)
            {
                isValidateState = false;
                this.textBox2.Lines = new string[0];
                this.button1.Text = "Stop learning...";
                this.HandleEmotions(this.textBox1);
            }
            else
            {
                stop = true;

                this.button1.Text =  "Learn" ;
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            this.isLearnState = false;
            stop = true;
            this.button1.Text = "Learn";
            this.textBox1.Lines = new string[0];
        }

        private void textBox2_TextChanged(object sender, EventArgs e)
        {

        }

        private void button3_Click(object sender, EventArgs e)
        {
            this.isLearnState = !this.isLearnState;
            if (this.isLearnState)
            {
                isValidateState = false;
                this.textBox2.Lines = new string[0];
                this.button3.Text = "Stop validating...";
                this.HandleEmotions(this.textBox2);
            }
            else
            {
                stop = true;

                this.button3.Text = "Enter key";
            }
        }

    }
}
