using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Web;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.IO;
using System.Net;
using System.Diagnostics;
using System.Xml;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading;

namespace ez_wretch2pixnet
{
    public partial class wretch2pixnet : Form
    {

        //請申請自用開發APP 得到相關資訊後填入  參考 http://apps.pixnet.tw 
        public string ConsumerKey = "";
        public string ComsumerSecret = "";

        public string request_token_url = "http://emma.pixnet.cc/oauth/request_token";
        public string access_token_url = "http://emma.pixnet.cc/oauth/access_token";

        public string oauth_token = "";
        public string oauth_token_secret = "";

        public bool add_oauth_verifier = false;
        public string oauth_verifier = "";

        public bool add_params = false;
        public Dictionary<string, string> add_params_list = new Dictionary<string, string>();

        public string App_dir = System.Windows.Forms.Application.StartupPath;

        public string log_folder = "";

        public wretch2pixnet()
        {
            InitializeComponent();
            init();
        }


        #region 工具類型

        public string _params_str = "";
        public string _params_str_header = "";

        public string get_basestring(string method, string url)
        {
            string tmp = "";
            _params_str = get_params_str();
            tmp = method + "&" + Uri.EscapeDataString(url) + "&" + Uri.EscapeDataString(_params_str.Replace("\"", "").Replace(",", "&"));
            return tmp;
        }

        public string get_params_str()
        {
            //固定會加的參數
            Dictionary<string, string> params_list = new Dictionary<string, string>();
            params_list.Add("oauth_consumer_key", ConsumerKey);
            params_list.Add("oauth_signature_method", "HMAC-SHA1");

            if (add_oauth_verifier == true)
            {
                params_list.Add("oauth_verifier", oauth_verifier);
                add_oauth_verifier = false;
            }

            if (add_params == true)
            {
                foreach (KeyValuePair<string, string> i in add_params_list)
                    params_list.Add(i.Key, i.Value);
                add_params_list.Clear();
                add_params = false;
            }

            params_list.Add("oauth_version", "1.0");
            params_list.Add("oauth_nonce", Convert.ToBase64String(new ASCIIEncoding().GetBytes(DateTime.Now.Ticks.ToString())));
            params_list.Add("oauth_timestamp", Convert.ToInt64((DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds).ToString());

            if (oauth_token != "")
                params_list.Add("oauth_token", oauth_token);
            string params_str = "";
            foreach (KeyValuePair<string, string> i in params_list.OrderBy(key => key.Key))
                params_str += i.Key + "=\"" + rfc3986_Escape(Uri.EscapeDataString(i.Value)) + "\",";
            params_str = params_str.Substring(0, params_str.Length - 1);

            Dictionary<string, string> params_list_header = new Dictionary<string, string>();
            params_list_header.Add("oauth_version", "1.0");
            params_list_header.Add("oauth_nonce", params_list["oauth_nonce"]);
            params_list_header.Add("oauth_timestamp", params_list["oauth_timestamp"]);
            params_list_header.Add("oauth_consumer_key", ConsumerKey);
            params_list_header.Add("oauth_signature_method", "HMAC-SHA1");
            if (oauth_token != "")
                params_list_header.Add("oauth_token", params_list["oauth_token"]);

            if (params_list.ContainsKey("oauth_verifier"))
                params_list_header.Add("oauth_verifier", oauth_verifier);

            _params_str_header = "";
            foreach (KeyValuePair<string, string> i in params_list_header)
                _params_str_header += i.Key + "=\"" + rfc3986_Escape(Uri.EscapeDataString(i.Value)) + "\",";
            _params_str_header = _params_str_header.Substring(0, _params_str_header.Length - 1);

            return params_str;
        }

        public string get_oauth_signature(string method, string url, string Comsumer_Secret, string oauth_token_secret)
        {
            string tmp = "";
            string basestring = get_basestring(method, url);
            string key = Comsumer_Secret + "&" + oauth_token_secret;
            var enc = Encoding.ASCII;
            HMACSHA1 hmac = new HMACSHA1(enc.GetBytes(key));
            hmac.Initialize();
            byte[] buffer = enc.GetBytes(basestring);
            tmp = Uri.EscapeDataString(Convert.ToBase64String(hmac.ComputeHash(buffer)).Replace("-", ""));
            return tmp;
        }

        public string get_oauth_header(string oauth_signature)
        {
            return "OAuth " + _params_str_header + ",oauth_signature=\"" + oauth_signature + "\"";
        }

        public string get_http_get(string url)
        {
            //認證字串
            string auth_str = get_oauth_header(get_oauth_signature("GET", url, ComsumerSecret, oauth_token_secret));
            string tmp = "";

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Proxy = null;
            request.Method = "GET";
            request.Timeout = 30000;

            if (auth_str != "")
                request.Headers.Add("Authorization", auth_str);

            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            Stream resStream = response.GetResponseStream();
            StreamReader reader = new StreamReader(resStream, Encoding.UTF8);
            tmp = reader.ReadToEnd();

            return tmp;
        }

        public string get_http_post_with_picture(string url, Dictionary<string, string> params_list, string picture_filepath)
        {

            string auth_str = get_oauth_header(get_oauth_signature("POST", "http://emma.pixnet.cc/album/elements", ComsumerSecret, oauth_token_secret));

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "POST";
            request.Proxy = null;
            request.Timeout = 30000;
            request.Headers.Add("Authorization", auth_str);

            //準備寫入資料 start
            string boundary = "----------------------------" + DateTime.Now.Ticks.ToString("x");
            byte[] boundarybytes = System.Text.Encoding.ASCII.GetBytes("\r\n--" + boundary + "\r\n");
            string formdataTemplate = "\r\n--" + boundary + "\r\nContent-Disposition: form-data; name=\"{0}\";\r\n\r\n{1}";

            Stream memStream = new System.IO.MemoryStream();

            foreach (string key in params_list.Keys)
            {
                string formitem = string.Format(formdataTemplate, key, params_list[key]);
                byte[] formitembytes = System.Text.Encoding.UTF8.GetBytes(formitem);
                memStream.Write(formitembytes, 0, formitembytes.Length);
            }
            memStream.Write(boundarybytes, 0, boundarybytes.Length);

            string headerTemplate = "Content-Disposition: form-data; name=\"upload_file\"; filename=\"{0}\"\r\nContent-Type: application/octet-stream\r\n\r\n";
            FileInfo fi = new FileInfo(picture_filepath);
            string header = string.Format(headerTemplate, fi.Name);
            byte[] headerbytes = System.Text.Encoding.UTF8.GetBytes(header);
            memStream.Write(headerbytes, 0, headerbytes.Length);

            FileStream fileStream = new FileStream(picture_filepath, FileMode.Open, FileAccess.Read);
            byte[] buffer = new byte[1024];
            int bytesReads = 0;
            while ((bytesReads = fileStream.Read(buffer, 0, buffer.Length)) != 0)
                memStream.Write(buffer, 0, bytesReads);
            fileStream.Close();

            memStream.Write(boundarybytes, 0, boundarybytes.Length - 2);
            memStream.Write(Encoding.Default.GetBytes("--\r\n".ToCharArray()), 0, 4);

            request.ContentLength = memStream.Length;
            memStream.Position = 0;
            byte[] tempBuffer = new byte[memStream.Length];

            long allbytes = memStream.Length;

            memStream.Read(tempBuffer, 0, tempBuffer.Length);
            memStream.Close();
            //準備寫入資料 end

            request.ContentType = "multipart/form-data; boundary=" + boundary;
            Stream requestStream = request.GetRequestStream();
            Stream write_stream = new MemoryStream(tempBuffer);
            int buffsize = 10240;  // 10KByte
            byte[] content = new byte[buffsize];
            int bytesRead = 0;

            this.Invoke((MethodInvoker)delegate
            {
                //progressBar1.Value = 0;

            });
            int p = 0;
            long c = 0;
            do
            {
                bytesRead = write_stream.Read(content, 0, content.Length);


                requestStream.Write(content, 0, bytesRead);

                c += bytesRead;
                this.Invoke((MethodInvoker)delegate
                {

                    //progressBar1.Value = (int)(100 * ((double)c / (double)allbytes));
                    //Thread.Sleep(100);
                    //label9.Text = progressBar1.Value.ToString() + " %";
                    p = (int)(390.0 * ((double)c / (double)allbytes));
                    label10.Width = p;
                    //progressBar1.Value;
                    label9.Text = ((int)(100 * (double)c / (double)allbytes)).ToString() + " %";

                });



            }
            while (bytesRead > 0);
            requestStream.Close();

            //Thread.Sleep(10);

            //MessageBox.Show("l" );



            this.Invoke((MethodInvoker)delegate
            {
                //   this.Refresh();
            });

            string res_str = "";

            try
            {
                HttpWebResponse request_res = (HttpWebResponse)request.GetResponse();
                Stream stream_res = request_res.GetResponseStream();
                StreamReader reader_stream = new StreamReader(stream_res);
                res_str = reader_stream.ReadToEnd();

                reader_stream.Close();
                stream_res.Close();
                request_res.Close();
                request = null;

            }
            catch
            {
            }

            return res_str;
        }

        public string get_http_post(string url, Dictionary<string, string> params_list)
        {
            string auth_str = get_oauth_header(get_oauth_signature("POST", url, ComsumerSecret, oauth_token_secret));

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "POST";
            request.Proxy = null;
            request.Timeout = 30000;
            request.Headers.Add("Authorization", auth_str);

            //準備寫入資料 start
            string boundary = "----------------------------" + DateTime.Now.Ticks.ToString("x");
            byte[] boundarybytes = System.Text.Encoding.ASCII.GetBytes("\r\n--" + boundary + "\r\n");
            string formdataTemplate = "\r\n--" + boundary + "\r\nContent-Disposition: form-data; name=\"{0}\";\r\n\r\n{1}";

            Stream memStream = new System.IO.MemoryStream();

            foreach (string key in params_list.Keys)
            {
                string formitem = string.Format(formdataTemplate, key, params_list[key]);
                byte[] formitembytes = System.Text.Encoding.UTF8.GetBytes(formitem);
                memStream.Write(formitembytes, 0, formitembytes.Length);
            }
            memStream.Write(boundarybytes, 0, boundarybytes.Length);

            memStream.Write(Encoding.Default.GetBytes("--\r\n".ToCharArray()), 0, 4);

            request.ContentLength = memStream.Length;
            memStream.Position = 0;
            byte[] tempBuffer = new byte[memStream.Length];
            memStream.Read(tempBuffer, 0, tempBuffer.Length);
            memStream.Close();
            //準備寫入資料 end

            request.ContentType = "multipart/form-data; boundary=" + boundary;
            Stream requestStream = request.GetRequestStream();
            Stream write_stream = new MemoryStream(tempBuffer);
            int buffsize = 1024;  // 1KByte
            byte[] content = new byte[buffsize];
            int bytesRead = 0;
            do
            {
                bytesRead = write_stream.Read(content, 0, content.Length);
                requestStream.Write(content, 0, bytesRead);
            }
            while (bytesRead > 0);
            requestStream.Close();

            string res_str = "";

            try
            {
                HttpWebResponse request_res = (HttpWebResponse)request.GetResponse();
                
                Stream stream_res = request_res.GetResponseStream();
                StreamReader reader_stream = new StreamReader(stream_res);
                res_str = reader_stream.ReadToEnd();
                reader_stream.Close();
                stream_res.Close();
                request_res.Close();
                request = null;

            }
            catch ( Exception e )
            {
                MessageBox.Show(url + " : " + e.Message);
            }

            return res_str;
        }

        public Dictionary<string, string> get_res_params(string res)
        {
            Dictionary<string, string> tmp = new Dictionary<string, string>();

            List<string> tmp_tokens = res.Split(new char[] { '&' }).ToList();

            foreach (string i in tmp_tokens)
            {
                List<string> tmp_item = i.Split(new char[] { '=' }).ToList();
                tmp.Add(tmp_item[0], HttpUtility.UrlDecode(tmp_item[1]));
            }

            return tmp;
        }

        public void copy_to_add_params_list(Dictionary<string, string> tmp)
        {
            foreach (string i in tmp.Keys)
                add_params_list.Add(i, tmp[i]);
        }

        public string rfc3986_Escape(string str)
        {
            /*
             * rfc3986 有以下保留字元 , Uri.EscapeDataString 會少處理部份字元的轉換,要自行增加
             * reserved    = gen-delims / sub-delims
             * gen-delims  = ":" / "/" / "?" / "#" / "[" / "]" / "@"
             * sub-delims  = "!" / "$" / "&" / "'" / "(" / ")" / "*" / "+" / "," / ";" / "="
             */
            return str.Replace("!", "%21").Replace("'", "%27").Replace("(", "%28").Replace(")", "%29").Replace("*", "%2A");
        }

        //http://stackoverflow.com/questions/12821998/how-to-generate-a-utc-unix-timestamp-in-c-sharp
        public string get_timestamp(DateTime value)
        {

            TimeSpan span = (value - new DateTime(1970, 1, 1, 0, 0, 0, 0).ToLocalTime());
            return span.TotalSeconds.ToString();
        }
        #endregion

        private void wretch2pixnet_Shown(object sender, EventArgs e)
        {
        }

        public void init()
        {

            log_folder = App_dir + @"\status_log";

            XmlDocument xml = new XmlDocument();
            xml.Load(App_dir + "\\configure.xml");

            if (xml["setup"]["oauth_token"].InnerText != "")
                oauth_token = xml["setup"]["oauth_token"].InnerText;

            if (xml["setup"]["oauth_token_secret"].InnerText != "")
                oauth_token_secret = xml["setup"]["oauth_token_secret"].InnerText;

            if (oauth_token != "" && oauth_token_secret != "")
            {
                textBox1.Enabled = false;
                button1.Enabled = false;
                button2.Enabled = false;
                button3.Enabled = true;
                groupBox2.Enabled = true;
                label2.Text = "認證狀態 - 通過認證";
            }

            textBox2.Text = xml["setup"]["local_wretch_folder"].InnerText;
            textBox3.Text = xml["setup"]["pixnet_folder_id"].InnerText;
            textBox4.Text = xml["setup"]["wretch_blog_xml"].InnerText;

            textBox6.Text = xml["setup"]["blog_class_id"].InnerText.Replace(" ", "");
            textBox5.Text = xml["setup"]["tag"].InnerText.Replace(" ", "");

            if( xml["setup"]["begin_importing_time"].InnerText != "")
                dateTimePicker1.Value = new DateTime(1970, 1, 1, 0, 0, 0, 0).AddSeconds(double.Parse(xml["setup"]["begin_importing_time"].InnerText) ).ToLocalTime();


        }



        private void button4_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog fb = new FolderBrowserDialog();
            if (fb.ShowDialog() != DialogResult.OK)
                return;

            textBox2.Text = fb.SelectedPath;

            XmlDocument xml = new XmlDocument();
            xml.Load(App_dir + "\\configure.xml");

            xml["setup"]["local_wretch_folder"].InnerText = fb.SelectedPath;
            xml.Save(App_dir + "\\configure.xml");
        }

        private void button1_Click(object sender, EventArgs e)
        {
            oauth_token = "";
            oauth_token_secret = "";

            string res = get_http_get(request_token_url);
            Dictionary<string, string> res_params = get_res_params(res);

            oauth_token = res_params["oauth_token"];
            oauth_token_secret = res_params["oauth_token_secret"];

            MessageBox.Show("將把你引導至認證頁面,登入後頁面會給你安全碼,請填入本軟體 [安全碼碼] 欄位");
            Process.Start(res_params["xoauth_request_auth_url"]);
        }

        private void button2_Click(object sender, EventArgs e)
        {
            add_oauth_verifier = true;
            oauth_verifier = textBox1.Text.Replace(" ", "");

            string res = get_http_get(access_token_url);

            Dictionary<string, string> res_params = get_res_params(res);

            try
            {
                oauth_token = res_params["oauth_token"];
                oauth_token_secret = res_params["oauth_token_secret"];
            }
            catch
            {
            }


            if (oauth_token != "" && oauth_token_secret != "")
            {

                XmlDocument xml = new XmlDocument();
                xml.Load(App_dir + "\\configure.xml");
                xml["setup"]["oauth_token"].InnerText = oauth_token;
                xml["setup"]["oauth_token_secret"].InnerText = oauth_token_secret;
                xml.Save(App_dir + "\\configure.xml");


                textBox1.Enabled = false;
                button1.Enabled = false;
                button2.Enabled = false;
                button3.Enabled = true;
                groupBox2.Enabled = true;

                label2.Text = "認證狀態 - 通過認證";

                textBox1.Text = "";

                MessageBox.Show("驗證過程完成");
            }
            //驗證步驟以上結束        
        }

        private void button3_Click(object sender, EventArgs e)
        {
            XmlDocument xml = new XmlDocument();
            xml.Load(App_dir + "\\configure.xml");
            xml["setup"]["oauth_token"].InnerText = "";
            xml["setup"]["oauth_token_secret"].InnerText = "";
            xml.Save(App_dir + "\\configure.xml");


            textBox1.Enabled = true;
            textBox1.Text = "";
            button1.Enabled = true;
            button2.Enabled = true;
            button3.Enabled = false;
            groupBox2.Enabled = false;
            label2.Text = "認證狀態 - 尚未通過";



        }



        private void wretch2pixnet_FormClosing(object sender, FormClosingEventArgs e)
        {

            XmlDocument xml = new XmlDocument();
            xml.Load(App_dir + "\\configure.xml");

            xml["setup"]["wretch_blog_xml"].InnerText = textBox4.Text;
            xml["setup"]["pixnet_folder_id"].InnerText = textBox3.Text;
            xml["setup"]["blog_class_id"].InnerText = textBox6.Text;
            xml["setup"]["tag"].InnerText = textBox5.Text;

            TimeSpan span = (dateTimePicker1.Value - new DateTime(1970, 1, 1, 0, 0, 0, 0).ToLocalTime());
            xml["setup"]["begin_importing_time"].InnerText = ((int)(span.TotalSeconds)).ToString();

            xml.Save(App_dir + "\\configure.xml");

            

            //return the total seconds (which is a UNIX timestamp)
            //return (double)span.TotalSeconds;
           //dateTimePicker1.Value

        }

        private void button8_Click(object sender, EventArgs e)
        {
            OpenFileDialog fd = new OpenFileDialog();

            fd.Filter = "XML (.xml)|*.xml";

            if (fd.ShowDialog() != DialogResult.OK)
                return;

            textBox4.Text = fd.FileName;


        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start("http://dl.dropboxusercontent.com/u/61164954/project/ezWretch2Pixnet/index.htm");
        }




        private void button5_Click(object sender, EventArgs e)
        {
            string wretch_folder = textBox2.Text;
            if (wretch_folder == "")
            {
                MessageBox.Show("請選擇照片所在目錄位置");
                return;
            }

            string pixnet_folder_id = textBox3.Text;

            string res = "";

            new Thread(() =>
            {

                this.Invoke((MethodInvoker)delegate
                {
                    button5.Enabled = false;

                });


                #region 處理相簿建立
                List<string> folders = Directory.GetDirectories(wretch_folder).ToList();

                List<string> reindex_folders = new List<string>();

                Dictionary<string, string> tmp_list = new Dictionary<string, string>();

                foreach (string i in folders)
                {
                    string folder_item = i.Remove(0, wretch_folder.Length + 1);
                    string id = folder_item.Split(new char[] { '-' })[0];
                    tmp_list.Add(int.Parse(id).ToString("D4"), folder_item);
                }

                foreach (KeyValuePair<string, string> i in tmp_list.OrderBy(key => key.Key))
                    reindex_folders.Add(i.Value);

                Dictionary<string, string> params_list = new Dictionary<string, string>();

                List<string> logs = new List<string>();
                foreach (string i in reindex_folders)
                {
                    string folder_item = i; // i.Remove(0, wretch_folder.Length + 1);
                    string id = folder_item.Split(new char[] { '-' })[0];
                    string org_name = folder_item.Split(new char[] { '-' })[1];

                    if (File.Exists(log_folder + @"\album\ok-" + id +".log" ))
                    {
                        this.Invoke((MethodInvoker)delegate
                        {
                            label5.Text = "處理資訊 - [" + org_name + "] 已經建立過.";
                            Thread.Sleep(40);
                        });
                        continue;
                    }


                    params_list.Add("title", "原無名 - " + org_name);
                    params_list.Add("description", org_name);

                    if (pixnet_folder_id != "")
                        params_list.Add("parent_id", pixnet_folder_id);

                    params_list.Add("permission", "0");
                    copy_to_add_params_list(params_list);
                    add_params = true;

                    res = get_http_post("http://emma.pixnet.cc/album/sets", params_list);
                    params_list.Clear();

                    JObject obj = JObject.Parse(res);
                    string album_id = "";

                    try
                    {
                        album_id = (string)obj["set"]["id"];

                        File.Create(log_folder + @"\album\ok-" + id + ".log").Close();

                        File.WriteAllText(log_folder + @"\album\ok-" + int.Parse(id).ToString("D4") + ".id", album_id + "|" + id + "|" + folder_item + "|" + org_name, Encoding.UTF8);

                        //File.AppendAllText(log_folder + @"\pixnet_album_mapping.txt", album_id + "|" + id + "|" + folder_item + "|" + org_name, Encoding.UTF8);

                    }
                    catch
                    {
                        this.Invoke((MethodInvoker)delegate
                        {
                            button5.Enabled = true;
                        });
                        MessageBox.Show("失敗,請重新執行");
                        return;
                    }
                    //logs.Add(album_id + "|" + id + "|" + folder_item + "|" + org_name);

                    this.Invoke((MethodInvoker)delegate
                    {

                        label5.Text = "處理資訊 - [" + org_name + "] 建立完成.    ";
                    });
                }


                foreach (string i in Directory.GetFiles(log_folder + @"\album"))
                {
                    try
                    {
                        FileInfo fi = new FileInfo(i);
                        if (fi.Extension == ".id")
                        {
                            logs.Add(File.ReadAllText(i, Encoding.UTF8));
                        }
                           
                    }
                    catch
                    {
                    }
                }

                File.WriteAllLines(log_folder + @"\pixnet_album_mapping.txt", logs, Encoding.UTF8);
                //MessageBox.Show("ok");
                //File.AppendAllLines(log_folder + @"\pixnet_album_mapping.txt", logs, Encoding.UTF8);

                #endregion

                this.Invoke((MethodInvoker)delegate
                {
                    label5.Text = "處理資訊 - 相簿批次建立轉匯作業完成!";
                    button5.Enabled = true;
                });

                MessageBox.Show("相簿批次建立轉匯作業完成!");

            }).Start();
        }

        private void button6_Click(object sender, EventArgs e)
        {
            try
            {
                File.Delete(log_folder + @"\pixnet_album_mapping.txt");
            }
            catch
            {
            }

            foreach (string i in Directory.GetFiles(log_folder + @"\album"))
            {
                try
                {
                    FileInfo fi = new FileInfo(i);
                    if (fi.Extension == ".id"  || fi.Extension == ".log")
                    {
                        File.Delete(i);
                    }

                }
                catch
                {
                }
            }


            MessageBox.Show("清空完成");

        }

        private void button10_Click(object sender, EventArgs e)
        {
            #region 處理照片上傳

            string wretch_folder = textBox2.Text;

            button1.Enabled = false;

            new Thread(() =>
            {


                StreamReader sr = File.OpenText(log_folder + @"\pixnet_album_mapping.txt");
                string alltext = sr.ReadToEnd();
                sr.Close();

                alltext = alltext.Remove(alltext.Length - 1);

                List<string> lines = alltext.Split(new char[] { '\n' }).ToList();

                string res = "";


                Dictionary<string, string> folder_mapping = new Dictionary<string, string>();
                Dictionary<string, string> params_list = new Dictionary<string, string>();


                foreach (string i in lines)
                    folder_mapping.Add(i.Split(new char[] { '|' })[2], i.Split(new char[] { '|' })[0]);

                foreach (KeyValuePair<string, string> j in folder_mapping.OrderBy(key => key.Key))
                {

                    //進入目錄內檔案處理層
                    List<string> files = Directory.GetFiles(wretch_folder + "\\" + j.Key).ToList();

                    foreach (string f in files)
                    {
                        string file_item = f.Remove(0, wretch_folder.Length + 1); //第一資訊

                        FileInfo fi = new FileInfo(f);

                        if (File.Exists(log_folder + "\\picture\\" + "ok-" + file_item.Replace("\\", "-"))) //代表此檔已經順利上傳處理
                        {
                            this.Invoke((MethodInvoker)delegate
                            {
                                label8.Text = "處理資訊 - 目錄 [" + j.Key + "] 下 " + "[" + fi.Name + "] " + "已上傳過   ";
                                Thread.Sleep(10);
                            });
                            continue;
                        }

                        //上傳 start
                        params_list.Add("set_id", j.Value);
                        params_list.Add("title", fi.Name.Replace(".jpg", ""));
                        copy_to_add_params_list(params_list);
                        add_params = true;

                        this.Invoke((MethodInvoker)delegate
                        {
                            label11.Text = fi.Name + " 上傳中...";
                        });
                        res = get_http_post_with_picture("http://emma.pixnet.cc/album/elements", params_list, f);
                        params_list.Clear();
                        //上傳 end

                        JObject obj = JObject.Parse(res);
                        string pic_id = "";
                        string pic_identifier = "";
                        string pic_result = "";

                        try
                        {
                            pic_result = (string)obj["message"];
                            pic_id = (string)obj["element"]["id"]; // 第二資訊
                            pic_identifier = (string)obj["element"]["identifier"]; // 第三資訊
                        }
                        catch
                        {
                            MessageBox.Show("上傳失敗,請重新執行照片轉匯作業!");
                            this.Invoke((MethodInvoker)delegate
                            {
                                button10.Enabled = true;
                            });
                            return;
                        }


                        if (pic_result != "圖片上傳成功")
                        {
                            MessageBox.Show("上傳失敗,請重新執行照片轉匯作業!");
                            return;
                        }


                        File.CreateText(log_folder + "\\picture\\" + "ok-" + file_item.Replace("\\", "-")).Close();


                        StreamWriter sw = File.AppendText(log_folder + "\\picture_mapping\\" + j.Key.Split(new char[] { '-' })[0] + ".txt");
                        sw.WriteLine(pic_id + "|" + pic_identifier + "|" + fi.Name);
                        sw.Close();

                        this.Invoke((MethodInvoker)delegate
                        {
                            label8.Text = "處理資訊 - 目錄 [" + j.Key + "] 下 " + "[" + fi.Name + "] " + "上傳完成";
                        });



                        this.Invoke((MethodInvoker)delegate
                        {
                            button10.Enabled = true;
                        });

                        //MessageBox.Show(" 處理完成");

                        //res = get_http_get("http://emma.pixnet.cc/account");
                        //obj = JObject.Parse(res);
                        //MessageBox.Show(res);


                    }
                }



                this.Invoke((MethodInvoker)delegate
                {
                    label8.Text = "處理資訊 - 照片轉匯作業完成!";
                });

                MessageBox.Show("照片轉匯作業完成!");

            }).Start();

            #endregion

        }

        private void button7_Click(object sender, EventArgs e)
        {

            int c = 0;
            DateTime t = new DateTime(2012, 1, 1, 1, 1, 1, 0, DateTimeKind.Utc);





            if (textBox4.Text == "")
            {
                MessageBox.Show("請填入無名Blog XML備份檔位置");
                return;
            }

            XmlDocument xml = null;
            xml = new XmlDocument();

            try
            {
                xml.Load(textBox4.Text);
            }
            catch
            {
                MessageBox.Show("數據檔錯誤!");
                return;
            }


            FileInfo fi = new FileInfo(textBox4.Text);

            if (!File.Exists(log_folder + "\\" + fi.Name + ".json"))
            {
                string json = JsonConvert.SerializeXmlNode(xml);
                File.WriteAllText(log_folder + "\\" + fi.Name + ".json", json, Encoding.UTF8);
            }

            string class_id = textBox6.Text;
            string tag = textBox5.Text;

            button7.Enabled = false;

            new Thread(() =>
            {

                StreamReader sr = File.OpenText(log_folder + "\\" + fi.Name + ".json");
                string all_str = sr.ReadToEnd();

                Dictionary<string, string> params_list = new Dictionary<string, string>();

                JObject obj = JObject.Parse(all_str);
                foreach (JObject i in obj["blog_backup"]["blog_articles"]["article"])
                {
                    string title = i["title"]["#cdata-section"].ToString();
                    string text = i["text"]["#cdata-section"].ToString();
                    string wretch_article_id = i["id"]["#cdata-section"].ToString();
                    string post_time = i["PostTime"]["#cdata-section"].ToString();


                    //內文連結置換
                    //  {###_baxermux/41/1035031792.jpg_###}
                    //  <img src="P1010564-hdr-d-s.jpg" />
                    // 244000622|1380701181-3210802450|1461652454-P1190121_filtered-s.jpg

                    List<string> need_replaces = new List<string>();
                    List<string> tmp_s = text.Split(new string[] { @"{###_" }, StringSplitOptions.None).ToList();
                    if (tmp_s.Count >= 2)
                    {
                        tmp_s.RemoveAt(0);
                        foreach (string s in tmp_s)
                            need_replaces.Add(s.Split(new string[] { @"_###}" }, StringSplitOptions.None).ToList()[0]);
                    }
                    text = text.Replace(@"{###_", "").Replace(@"_###}", "");
                    string idf = "";
                    foreach (string r in need_replaces)
                    {
                        string dir_id = r.Split(new char[] { '/' })[1];
                        string org_wretch_id = r.Split(new char[] { '/' })[2].Replace(".jpg", "");

                        if (!File.Exists(log_folder + "\\picture_mapping\\" + dir_id + ".txt"))
                        {
                            //text = text.Replace(r, "<img src=\"\" />");
                            //text = "<img alt=\"圖片遺失\" src=\"data:image/x-png;base64,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\" />";
                            break;

                        }

                        StreamReader srr = File.OpenText(log_folder + "\\picture_mapping\\" + dir_id + ".txt");
                        string all = srr.ReadToEnd();

                        List<string> lines = all.Split(new char[] { '\n' }).ToList();

                        idf = "";
                        foreach (string p in lines)
                        {
                            Match match = Regex.Match(p, org_wretch_id, RegexOptions.IgnoreCase);
                            if (match.Success)
                            {
                                idf = p.Split(new char[] { '|' })[1];
                                break;
                            }
                        }

                        if (idf != "")
                        {
                            string loc_new = "http://pic.pimg.tw/baxermux/" + idf + ".jpg";
                            text = text.Replace(r, "<img src=\"" + loc_new + "\" />");
                        }
                        //else
                        //{
                          //  text = text.Replace(r, "<img src=\"\" />");
                            //text = "<img alt=\"圖片遺失\" src=\"data:image/x-png;base64,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\" />";
                        //}

                        //text.Replace(r, "<img src=\"" + "" + "\" />");
                    }

                    if (File.Exists(log_folder + "\\blog\\ok-" + post_time.Replace(":","-" ) + ".log" ) )
                    {
                        this.Invoke((MethodInvoker)delegate
                        {

                            label6.Text = "["+title + "] 已匯入過";
                            Thread.Sleep(100);
                        
                        });
                        //Console.WriteLine(title + "已匯入過");
                        continue;
                    }

                    params_list.Add("title", "(原無名 "+ post_time +" ) " + title);//標題
                    params_list.Add("body", text); //內文
                    params_list.Add("status", "2");//狀態 2 公開
                    params_list.Add("public_at", get_timestamp(t.AddSeconds(c))); // unix timestamp
                    params_list.Add("comment_perm", "1");//關閉留言

                    if( class_id != "")
                        params_list.Add("category_id", class_id ); //個人分類

                    if( tag != "")
                        params_list.Add("tags", "開發測試");//標籤

                    c++;

                    copy_to_add_params_list(params_list);
                    add_params = true;
                    string res = get_http_post("http://emma.pixnet.cc/blog/articles", params_list);

                    JObject ri = null;

                    try
                    {
                        ri = JObject.Parse(res);
                    }
                    catch
                    {
                   
                        MessageBox.Show("失敗,請重新執行!");
                        this.Invoke((MethodInvoker)delegate
                        {
                            button7.Enabled = true;
                        });
                        return;
                    }

                    //MessageBox.Show(ri["message"].ToString());

                    string pixnet_article_id = ri["article"]["id"].ToString();
                    params_list.Clear();

                    this.Invoke((MethodInvoker)delegate
                    {
                        label6.Text = "處理資訊 - [" + title + "] 文章匯入完成";
                    });

                    File.Create(log_folder + "\\blog\\ok-" + post_time.Replace(":", "-") + ".log").Close();

                    string message = "";

                    foreach (JObject j in obj["blog_backup"]["blog_articles_comments"]["article_comment"])
                    {
                        if (wretch_article_id == j["article_id"]["#cdata-section"].ToString())
                        {

                            string date = j["date"]["#cdata-section"].ToString();
                            string comment = HttpUtility.HtmlDecode(j["text"]["#cdata-section"].ToString().Replace(@"\r\n", "").Replace(@"<br />", ""));
                            string user_name = j["name"]["#cdata-section"].ToString();
                            string reply_text = HttpUtility.HtmlDecode(j["reply"]["#cdata-section"].ToString().Replace(@"\r\n", "").Replace(@"<br />", ""));
                            string replay_date = j["reply_date"]["#cdata-section"].ToString();


                            message = message + "======\n留言人 : " + user_name + "\n留言日期 : " + date + "\n留言內容 : \n" + comment + "\n";

                            if (reply_text != "")
                                message = message + "\n版主回覆 : \n" + reply_text + "\n\n回覆時間 : " + replay_date + "\n";

                        }
                    }

                    if (message != "")
                    {
                        //寫入留言內容下去
                        Dictionary<string, string> params_list_msg = new Dictionary<string, string>();

                        params_list_msg.Add("article_id", pixnet_article_id);
                        params_list_msg.Add("body", "原始無名留言內容轉匯\n" + message);


                        copy_to_add_params_list(params_list_msg);
                        add_params = true;

                        //Thread.Sleep(6000);

                        res = get_http_post("http://emma.pixnet.cc/blog/comments", params_list_msg);

                        params_list_msg.Clear();

                        this.Invoke((MethodInvoker)delegate
                        {
                            label6.Text = "處理資訊 - [" + title + "] 文章留言匯入完成";
                        });
                    }

                }

                this.Invoke((MethodInvoker)delegate
                {
                    button10.Enabled = true;
                });

                MessageBox.Show("文章轉匯完成");
            }).Start();


        }

        private void button9_Click(object sender, EventArgs e)
        {

        }

        private void button9_Click_1(object sender, EventArgs e)
        {
             
            foreach (string i in Directory.GetFiles(log_folder + @"\blog"))
            {
                try
                {
                    FileInfo fi = new FileInfo(i);
                    if (fi.Extension == ".log")
                        File.Delete(i);
                }
                catch
                {
                }
            }

            MessageBox.Show("清空完成");
        }

        private void button11_Click(object sender, EventArgs e)
        {

            foreach (string i in Directory.GetFiles(log_folder + @"\picture"))
            {
                try
                {
                    FileInfo fi = new FileInfo(i);
                    if (fi.Extension == ".jpg")
                        File.Delete(i);
                }
                catch
                {
                }
            }

            foreach (string i in Directory.GetFiles(log_folder + @"\picture_mapping"))
            {
                try
                {
                    FileInfo fi = new FileInfo(i);
                    if (fi.Extension == ".txt")
                        File.Delete(i);
                }
                catch
                {
                }
            }

            //log_folder + "\\picture_mapping

            MessageBox.Show("清空完成");
        }


    }
}
