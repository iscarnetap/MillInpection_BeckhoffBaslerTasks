using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Text;

namespace Inspection
{
    public class WebComm
    {
        public ListBox lstSend;
        public TextBox txtsend;
        string Server = "";
        string name = "";

        public Form frm;
        public struct SendRobotParms //====2013
        {
            public string comment;
            public Single[] SendParm;
            public bool NotSendMess;
            public string cmd;
            public int FunctionCode;
            public Single timeout;
            public int DebugTime;
            //public int FunctionCode;
        }
        public struct CommReply
        {
            public bool result;
            public float[] data;
            public string status;
            public string comment;//====2013
            public int FunctionCode;
            public string Error;
        }
        public struct HostReply
        {
            public bool result;
            public string reply;
            public string cmd;
            public string[] data;
            public string status;
            public string comment;//====2013
            public string error;
            public HttpListenerContext contex;

        }
        public DataIniFile dFile = new DataIniFile();
       
        public void SetControls(ListBox LstSend, Form Frm, WebBrowser WB)
        {
            lstSend = LstSend;
            //txtstate = Txtstate;
            frm = Frm;
            //WBnew = WB;
           

        }
        public void SetControls1(TextBox txtSend, Form Frm, string Name = "", string server = "")
        {
            txtsend = null;
            txtsend = txtSend;
            Server = server;
            frm = Frm;
            
            name = Name;

        }
        delegate void SetListText(string text, ListBox lst, Form frm);
        public void SetTextLst(string text, ListBox lst, Form frm)
        {
            
            if ((lst == null) || (frm == null)) { return; }

            try
            {
                if (lst.InvokeRequired)
                {
                    SetListText d = new SetListText(SetTextLst);
                    frm.Invoke(d, new object[] { text, lst, frm });
                }
                else
                {
                    lst.Items.Add(text);
                    if (lst.Items.Count > 100) { lst.Items.Clear(); }
                }
            }
            catch { }

        }
        delegate void SettxtText(string text, TextBox txt, Form frm);
        public void SetTexttxt(string text, TextBox txt, Form frm)
        {
            
            if ((txt == null) || (frm == null)) { return; }

            try
            {
                if (txt.InvokeRequired)
                {
                    SettxtText d = new SettxtText(SetTexttxt);
                    frm.Invoke(d, new object[] { text, txt, frm });
                }
                else
                {
                    text = name + " " + text;
                    txt.Text =  txt.Text + text+"\r\n"; 
                }
            }
            catch { }

        }
        /// <summary>

        //delegate void SetWB(WebBrowser wb,Form frm);
        //--------------------------------------------------------------------------------------------------
        #region client-server //----------------------/web client-server---------------------------------------------------------------------------

        private string serverEtag = Guid.NewGuid().ToString("N");
        HttpListener _httpListener = new HttpListener();
        CancellationTokenSource cancelToken = new CancellationTokenSource();

        

        public async Task<WebComm.HostReply> ReadHttp()
        {
            WebComm.HostReply reply = new WebComm.HostReply();
            reply.result = false;
            reply.comment = "";
            try
            {
                if (cancelToken != null) cancelToken.Dispose();
                cancelToken = new CancellationTokenSource();
                CancellationToken cancel1 = cancelToken.Token;
                
                //HttpListenerContext context1 = await Task.Run(() => _httpListener.GetContext(), cancel1);
                HttpListenerContext context1 = await Task.Run(() => _httpListener.GetContextAsync(), cancel1);

                string[] ss = context1.Request.Url.ToString().Trim().Split('?');

                if (ss.Length > 1)
                {
                    reply.comment = ss[1].Trim();
                    SetTexttxt("=>" + ss[1].Trim() + "// (" + DateTime.Now.ToString("HH:mm:ss.fff") + ")" , txtsend, frm);
                }
                else
                {
                    
                    SetTexttxt("=>"+ "error" + "// (" + DateTime.Now.ToString("HH:mm:ss.fff") + ")" , txtsend, frm);
                }


                reply.result = true;
                reply.contex = context1;
                return reply;


            }

            catch (TaskCanceledException ex) { if (_httpListener != null) _httpListener.Stop(); inv.settxt(txtsend, txtsend.Text + "Cancel HTTP server." ); bStop = true; return reply; }
            catch (Exception ex) { if(_httpListener!=null)_httpListener.Stop(); inv.settxt(txtsend, txtsend.Text + ex.Message ); bStop = true; return reply; }



        }
        public async Task<WebComm.HostReply> SendHttp(HttpListenerContext context, string mess)
        {
            WebComm.HostReply reply = new WebComm.HostReply();
            reply.result = false;
            reply.comment = "";
            try
            {
                if (cancelToken != null) cancelToken.Dispose();
                cancelToken = new CancellationTokenSource();
                CancellationToken cancel = cancelToken.Token;

                byte[] _responseArray = Encoding.UTF8.GetBytes(mess); // get the bytes to response
               
                SetTexttxt("<=" + mess + "// (" + DateTime.Now.ToString("HH:mm:ss.fff") + ")" , txtsend, frm);
                reply.comment = mess;
                if (context != null)
                {
                    context.Response.OutputStream.Write(_responseArray, 0, _responseArray.Length); // write bytes to the output stream
                    context.Response.OutputStream.Flush();

                    context.Response.KeepAlive = false; // set the KeepAlive bool to false
                    context.Response.Close(); // close the connection
                                              //inv.settxt(txtServer, txtServer.Text + "Respone given to a request." + "\r\n");
                                              //_httpListener.Stop();
                    reply.result = true;
                    return reply;
                }
                else
                {
                   
                    SetTexttxt("<=" + "Error Send" + "// (" + DateTime.Now.ToString("HH:mm:ss.fff") + ")", txtsend, frm);
                    return reply;
                }

            }

            catch (TaskCanceledException ex) { _httpListener.Stop(); inv.settxt(txtsend, txtsend.Text + "Cancel HTTP server." + "\r\n"); return reply; }
            catch (Exception ex) { _httpListener.Stop(); inv.settxt(txtsend, txtsend.Text + ex.Message + "\r\n"); return reply; }



        }
        public bool StartServer()
        {
            try
            {
                inv.settxt(txtsend, txtsend.Text + "Http Server Start "+ Server + "\r\n");
                if(_httpListener == null)  _httpListener = new HttpListener();
                _httpListener.Prefixes.Clear();
                _httpListener.Prefixes.Add(Server); // add prefix "http://localhost:5000/" "http://*:5000/"
                _httpListener.Start(); // start server (Run application as Administrator!)
                return true;
            }
            catch (Exception ex) { return false; }
        }
        public async Task<bool> HttpServer()
        {
            WebComm.HostReply reply1 = new WebComm.HostReply();

            try
            {
                inv.settxt(txtsend, txtsend.Text + "Http Server Start "+Server + "\r\n");

                _httpListener.Prefixes.Add(Server); // add prefix "http://localhost:5000/" "http://*:5000/"
                _httpListener.Start(); // start server (Run application as Administrator!)
                while (!bStop)
                {
                    var task = Task.Run(() => ReadHttp());
                    await task;
                    reply1 = task.Result;

                    var task1 = Task.Run(() => SendHttp(reply1.contex, reply1.comment + "!!!"));
                    await task1;
                    Thread.Sleep(1);
                    if (txtsend.Text.Length > 2000) inv.settxt(txtsend, "");


                }
                inv.settxt(txtsend, txtsend.Text + "Http Server Stopped" + "\r\n");
                _httpListener.Stop();
                return true;
            }
            catch (Exception ex) { inv.settxt(txtsend, txtsend.Text + "Error Http Server Start " + ex.Message + "\r\n"); return false; }
        }
        public Boolean bStop = false;
        public void Stop()
        {
            try
            {
                bStop = true;

                //context1.Response.Close();
                //context1.Response.Abort();
                //cancelToken.Cancel();
                //Thread.Sleep(100);
                if (_httpListener!=null &&_httpListener.IsListening) _httpListener.Stop();
                if (_httpListener != null) _httpListener.Close();
                if (_httpListener != null) _httpListener.Abort();
                _httpListener = null;
                //inv.set(btnSatrt2, "Enabled", true);
            }
            catch (Exception ex) { }
        }
        #endregion client-server//--------------------------------------web client-server--------------------------------------------------------------------

        //------------------------------------------------------------------------------------------------------







    }
}
