using Panacea.Core;
using Panacea.Modularity.Imprivata;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using System.Xml.Linq;
using Panacea.Modularity.UiManager;
using Panacea.Modules.Imprivata.Views;
using Panacea.Modules.Imprivata.ViewModels;

namespace Panacea.Modules.Imprivata
{
    public class ImprivataManager
    {
        private XElement _latestReply;
        private string _clientLocation;
        private readonly string _productId;
        private readonly PanaceaServices _core;
        private readonly string _serverUrl;

        public ImprivataManager(PanaceaServices core, string serverUrl, string productId)
        {
            _core = core;
            _serverUrl = serverUrl;
            _productId = productId;
        }
        public async Task<AuthenticationResult> AuthenticateWithCardCodeAsync(string code, ImprivataSettings settings)
        {
            try
            {
                string passw = await ShowPasswordPopupAsync("KOSTAS", "WTF IS MODALITY");
                //change card depending on imprivata installation
                var results = new List<KeyValuePair<int, XElement>>();
                Exception exception = null;
                List<string> codes = new List<string>() { code };
                if (!string.IsNullOrEmpty(settings?.TransformScript))
                {
                    try
                    {
                        var codes2 = Compiler.GetNumbers(settings?.TransformScript, code);
                        codes = codes.Concat(codes2).ToList();
                    }
                    catch (Exception ex)
                    {
                        _core.Logger.Error(this, ex.Message);
                    }
                }
                var tasks = new List<Task>();
                foreach (var str in codes)
                {
                    var t = new Task(() =>
                    {
                        try
                        {
                            var res = RequestCard(str);
                            var dispp = int.Parse(res.Element("AuthState").Attribute("disp").Value);
                            results.Add(new KeyValuePair<int, XElement>(dispp, res));
                        }
                        catch (Exception ex)
                        {
                            exception = ex;
                        }

                    });
                    t.Start();
                    tasks.Add(t);
                }

                Console.WriteLine(@"waiting for tasks to finish");
                Task.WaitAll(tasks.ToArray(), 20000);

                if (exception != null)
                {
                    _core.Logger.Error(this, exception?.StackTrace);
                    throw exception;
                }
                Console.WriteLine(@"----  all tasks finished");
                int disp = 2;
                if (results.Any(r => r.Key == 0))
                {
                    //yay
                    disp = 0;
                    _latestReply = results.First(r => r.Key == 0).Value;
                }
                else if (results.Any(r => r.Key == 1))
                {
                    //pass requested from 1 task
                    disp = 1;
                    _latestReply = results.First(r => r.Key == 1).Value;
                }
                else if (results.Any(r => r.Key == 2))
                {
                    //pass requested from 1 task
                    disp = 2;
                    _latestReply = results.First(r => r.Key == 2).Value;
                }
                switch (disp)
                {
                    case 0:
                        var authTicket = _latestReply.Element("AuthTicket").Value;
                        var user =
                            _latestReply.Element("Principal")
                                .Element("UserIdentity")
                                .Element("Username")
                                .Value;
                        var domain =
                            _latestReply.Element("Principal")
                                .Element("UserIdentity")
                                .Element("Domain")
                                .Value;
                        string usern = _latestReply.Element("Principal").Attribute("displayName").Value;
                        var pass = GetCredsFromAuthTicket(authTicket);
                        return new AuthenticationResult()
                        {
                            AuthenticationTicket = authTicket,
                            Domain = domain,
                            Password = pass,
                            Username = user,
                            Name = usern
                        };
                    case 1:
                        string state = _latestReply.Element("ServerState").Value;
                        var domain1 =
                            _latestReply.Element("Principal")
                                .Element("UserIdentity")
                                .Element("Domain")
                                .Value;
                        string modality =
                            _latestReply.Element("RemainingAuthPolicy")
                                .Element("AuthPolicyOption")
                                .Element("AuthPolicyItem")
                                .Attribute("modalityID")
                                .Value;
                        var user1 =
                            _latestReply.Element("Principal")
                                .Element("UserIdentity")
                                .Element("Username")
                                .Value;

                        string username = _latestReply.Element("Principal").Attribute("displayName").Value;
                        PasswordRequestEventArgs p = new PasswordRequestEventArgs()
                        {
                            ServerState = state,
                            Name = username,
                            Modality = modality,
                            Username = user1,
                            Domain = domain1
                        };
                        string password = await ShowPasswordPopupAsync(username, modality);
                        return await AuthenticateWithPasswordAsync(p, password);
                    default:
                        throw new AuthenticationException("Account does not exist");
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private XElement RequestCard(string code)
        {
            IPAddress[] hostIPs = Dns.GetHostAddresses(Dns.GetHostName());
            if (hostIPs.Any(hostIp => hostIp.ToString().Length < 16))
            {
                _clientLocation = "name=" + Dns.GetHostName();
            }

            var requestObj = MakeProxAuthRequestObject(code);
            EnableCertificateSkipping();

            var _latestReply = SendRestRequest("AuthUser", requestObj);
            return _latestReply;

        }
        
        private async Task<string> ShowPasswordPopupAsync(string username, string modality)
        {
            PasswordPopupViewModel p = new PasswordPopupViewModel(_core, username, modality);
            if (_core.TryGetUiManager(out IUiManager _uiManager))
            {
                return await _uiManager.ShowPopup<string>(p);
            }
            else
            {
                _core.Logger.Error(this, "ui manager not loaded");
                throw new AuthenticationException("ui manager not loaded");
            }
        }
        public Task<AuthenticationResult> AuthenticateWithPasswordAsync(PasswordRequestEventArgs args, string password)
        {
            return Task.Run(() =>
            {
                string user = args.Username;
                string domain = args.Domain;
                string state = args.ServerState;
                string modality = args.Modality;

                string requestObj = null;
                switch (modality)
                {
                    case "PWD":
                        requestObj = MakePasswordAuthRequestObject(user, password, domain, state);
                        break;
                    case "PIN":
                        requestObj = MakePINAuthRequestObject(password, state);
                        break;
                }
                var reply = SendRestRequest("AuthUser", requestObj);
                int disp = int.Parse(reply.Element("AuthState").Attribute("disp").Value);

                switch (disp)
                {
                    case 0:
                        var authTicket = reply.Element("AuthTicket").Value;

                        string pass = GetCredsFromAuthTicket(authTicket);

                        return new AuthenticationResult()
                        {
                            Username = args.Username,
                            Password = pass,
                            Domain = args.Domain,
                            AuthenticationTicket = authTicket,
                            Name = args.Name
                        };
                    case 1:
                        throw new AuthenticationException("additional auth required");
                    //addtional auth required.
                    default:
                        throw new AuthenticationException("incorrect credentials");
                        //OnWrongCredentials(args);
                }
            });
        }

        static string MakePINAuthRequestObject(string pin, string state = "")
        {
            var requestObj = String.Format(
                @"<Request>
                    <ServerState>{1}</ServerState>
                    <ModalityAuthInput modalityID=""PIN"">
                        <AuthRequest>
                            <PINVerificationRequest>
                                <PIN>{0}</PIN>
                            </PINVerificationRequest>
                        </AuthRequest>
                    </ModalityAuthInput>
                    <CreateAuthTicket>true</CreateAuthTicket>
                </Request>", pin, state);
            return requestObj;
        }
        static string MakePasswordAuthRequestObject(string user, string pass, string domain = "", string state = "")
        {
            var requestObj = String.Format(
                    @"<Request>
                <ServerState>{3}</ServerState>
                    <ModalityAuthInput modalityID=""PWD"">
                        <AuthRequest>
                            <PasswordVerificationRequest>
                                <UserIdentity>
                                    <Username>{0}</Username>
                                    <Domain>{1}</Domain>
                                </UserIdentity>
                                <Password>{2}</Password>
                            </PasswordVerificationRequest>
                        </AuthRequest>
                    </ModalityAuthInput>
                    <CreateAuthTicket>true</CreateAuthTicket>
                </Request>", user, domain, pass, state);
            return requestObj;
        }

        private string MakeProxAuthRequestObject(string uniqueId)
        {
            var authInput = String.Format(
                @"<Request>
                    <ModalityAuthInput modalityID=""UID"">
                        <AuthRequest>
                            <UniqueID>{0}</UniqueID>
                        </AuthRequest>
                    </ModalityAuthInput>
                    <CreateAuthTicket>true</CreateAuthTicket>
                </Request>",
                uniqueId);
            return authInput;
        }

        string GetCredsFromAuthTicket(string authid)
        {
            var webRequest = (HttpWebRequest)HttpWebRequest.Create(_serverUrl + "/sso/ProveIDWeb/v1/Password");
            webRequest.Method = "GET";
            webRequest.Timeout = 4000;
            webRequest.Accept = "text/xml";
            webRequest.ContentType = "text/xml";
            webRequest.Headers.Add("isx-product", _productId);
            //webRequest.Headers.Add("isx-client", clientLocation);
            webRequest.Headers.Add("Authorization", "OStick ostick.ticket=" + HttpUtility.UrlEncode(authid));
            XElement returnXml = null;
            using (var webResponse = (HttpWebResponse)webRequest.GetResponse())
            {
                using (var stream = webResponse.GetResponseStream())
                {
                    if (stream != null)
                    {
                        var xmlRead = XmlReader.Create(stream);
                        xmlRead.MoveToContent();
                        returnXml = (XElement)XNode.ReadFrom(xmlRead);
                        Console.WriteLine(returnXml.ToString());
                        xmlRead.Close();
                    }
                }
            }
            return ((XElement)returnXml).Element("UserIdentityPassword").Value;
        }

        XElement SendRestRequest(string resource, string content)
        {
            var webRequest = (HttpWebRequest)HttpWebRequest.Create(_serverUrl + "/sso/proveidweb/v1/" + resource);

            try
            {

                webRequest.Method = "POST";
                webRequest.Timeout = 4000;
                webRequest.Accept = "text/xml";
                webRequest.ContentType = "text/xml";
                webRequest.Headers.Add("isx-product", _productId);
                webRequest.Headers.Add("isx-client", _clientLocation);
                webRequest.Timeout = 5000;

                using (var stream = webRequest.GetRequestStream())
                using (var streamWrite = new StreamWriter(stream))
                {
                    streamWrite.Write(content);
                    streamWrite.Close();
                }
                XElement returnXml = null;
                using (var webResponse = (HttpWebResponse)webRequest.GetResponse())
                using (var stream = webResponse.GetResponseStream())
                {
                    if (stream != null)
                    {
                        var xmlRead = XmlReader.Create(stream);
                        xmlRead.MoveToContent();
                        returnXml = (XElement)XNode.ReadFrom(xmlRead);
                        Console.WriteLine(returnXml.ToString());
                        xmlRead.Close();
                    }
                }

                return returnXml;
            }
            catch (WebException ex)
            {

                try
                {
                    if (ex.Response != null)
                    {
                        using (var reader = new StreamReader(ex.Response.GetResponseStream()))
                        {
                            _core.Logger.Error(this, reader.ReadToEnd());
                        }
                    }
                }
                catch { }
                throw;
            }
        }

        static void EnableCertificateSkipping()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls11;
            ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, policyErrors) => true;
        }
    }

    public class PasswordRequestEventArgs
    {
        public string Name { get; set; }
        public string Username { get; set; }
        public string Modality { get; set; }
        public string Domain { get; set; }
        public string ServerState { get; set; }
    }
}
