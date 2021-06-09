﻿using Dapper;
using FFMPEG_Demo.Models;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Data.SQLite;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Http;
using System.Web.Http.Description;

namespace FFMPEG_Demo.Controllers
{
    //[EnableCorsAttribute("*", "*", "*")]
    public class MpegController : ApiController
    {
        [HttpGet]
        public HttpResponseMessage Index()
        {
            var obj = new { data = "Welcome to Mpeg API" };
            return Request.CreateResponse(HttpStatusCode.OK, obj);
        }

        #region Video Encryption
        [HttpGet]
        public HttpResponseMessage UniquID()
        {
            Guid g = Guid.NewGuid();
            string uid = Guid.NewGuid().ToString();
            string dt = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var obj = new { Id = dt + "-" + uid };
            return Request.CreateResponse(HttpStatusCode.OK, obj);
        }
        [HttpPost]
        public HttpResponseMessage CreateFolder(string name = null)
        {
            string _alert = null;
            //string base_content_storage = ConfigurationManager.AppSettings["base_content_storage"];
            string base_content_storage = GetStoragePath();
            string path = base_content_storage + name;
            if (!(Directory.Exists(path)))
            {
                Directory.CreateDirectory(path);
                _alert = "Directory Created Successfully";
            }
            else
            {
                _alert = "Directory Alraedy Existed";
            }
            var obj = new { alert = _alert };
            return Request.CreateResponse(HttpStatusCode.OK, obj);
        }
        [HttpPost]
        public HttpResponseMessage DeleteFolder(string name = null)
        {
            string _alert = null;
            string base_content_storage = GetStoragePath();
            //string base_content_storage = ConfigurationManager.AppSettings["base_content_storage"];
            string path = base_content_storage + name;
            if ((Directory.Exists(path)))
            {
                DeleteDirectory(path);
                _alert = "Directory Deleted Successfully";
            }
            else
            {
                _alert = "Directory does not existed.";
            }
            var obj = new { alert = _alert };
            return Request.CreateResponse(HttpStatusCode.OK, obj);
        }
        [HttpPost]
        public HttpResponseMessage CreateKey(CreateKeyBody createKeyBody)
        {
            //var uid = httpRequest.Params.GetValues("uid")[0];
            string _alert = null;
            if (!String.IsNullOrWhiteSpace(createKeyBody.Id))
            {
                string base_content_storage = GetStoragePath();
                //string base_content_storage = ConfigurationManager.AppSettings["base_content_storage"];
                string path = base_content_storage + createKeyBody.Id;
                if (!(Directory.Exists(path)))
                {
                    CreateFolder(createKeyBody.Id);
                }
                //open ssl cmd here
                RunSSL(createKeyBody.Id);
                Key2DB(createKeyBody.Id);
                _alert = "Key File Created Successfully";
            }
            else
            {
                _alert = "Please provide Content Id";
            }

            var obj = new { alert = _alert };
            return Request.CreateResponse(HttpStatusCode.OK, obj);
        }
        [HttpGet]
        public HttpResponseMessage Keyinfo(string id = null)
        {
            //var path = System.Web.HttpContext.Current.Server.MapPath("~/player_content/" + id);
            string base_content_storage = GetStoragePath();
            //string base_content_storage = ConfigurationManager.AppSettings["base_content_storage"];
            string path = base_content_storage + id;
            string OpenKey = null;
            var fullpath = Path.Combine(base_content_storage, id, "enc.key");
            string _alert = null;
            if (Directory.Exists(base_content_storage + id))
            {
                //string FFMpegCon = ConfigurationManager.ConnectionStrings["FFMpeg"].ConnectionString;
                //SqlConnection con = new SqlConnection(FFMpegCon);
                #region SQlite database
                string FFMpegCon = GetSQLiteConnection();
                if (string.IsNullOrEmpty(FFMpegCon))
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound, new { data = "no database found!" });
                }
                SQLiteConnection con = new SQLiteConnection(FFMpegCon);
                #endregion
                OpenKey = con.ExecuteScalar<string>("SELECT OpenKey FROM tblContent WHERE contentID = @contentID", new
                {
                    @contentID = id
                });
                if (!string.IsNullOrEmpty(OpenKey))
                {
                    byte[] OpenKeyB = Encoding.Default.GetBytes(OpenKey);
                    var dataStream = new MemoryStream(OpenKeyB);
                    HttpResponseMessage httpResponseMessage = Request.CreateResponse(HttpStatusCode.OK);
                    httpResponseMessage.Content = new StreamContent(dataStream);
                    httpResponseMessage.Content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment");
                    httpResponseMessage.Content.Headers.ContentDisposition.FileName = "enc.key";
                    httpResponseMessage.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                    return httpResponseMessage;
                }
                else
                {
                    _alert = "file not found";
                }
                /* if (File.Exists(fullpath))
                 {
                     var dataBytes = File.ReadAllBytes(fullpath);
                     var dataStream = new MemoryStream(dataBytes);
                     HttpResponseMessage httpResponseMessage = Request.CreateResponse(HttpStatusCode.OK);
                     httpResponseMessage.Content = new StreamContent(dataStream);
                     httpResponseMessage.Content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment");
                     httpResponseMessage.Content.Headers.ContentDisposition.FileName = "enc.key";
                     httpResponseMessage.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                     return httpResponseMessage;
                 }
                 else
                 {
                     _alert = "file not found";
                 }*/
            }
            else
            {
                _alert = "file not found";
            }
            var obj = new { alert = _alert };
            return Request.CreateResponse(HttpStatusCode.OK, obj);
        }
        [HttpGet]
        public HttpResponseMessage M3u8info(string id = null)
        {
            string Scheme = HttpContext.Current.Request.Url.Scheme;
            string Authority = HttpContext.Current.Request.Url.Authority;
            string baseurl = string.Format("{0}://{1}/", Scheme, Authority);
            string base_content_storage = GetStoragePath();

            //var path = System.Web.HttpContext.Current.Server.MapPath("~/player_content/" + id);
            //string base_content_storage = ConfigurationManager.AppSettings["base_content_storage"];
            string filename = "index.m3u8";
            var fullpath = Path.Combine(base_content_storage, id, filename);
            string _alert = null;
            if (Directory.Exists(base_content_storage + id))
            {
                if (File.Exists(fullpath))
                {
                    var dataBytes = File.ReadAllBytes(fullpath);
                    var dataStream = new MemoryStream(dataBytes);
                    dataStream = M3u8DataManipulation(dataStream, id, baseurl);
                    HttpResponseMessage httpResponseMessage = Request.CreateResponse(HttpStatusCode.OK);
                    httpResponseMessage.Content = new StreamContent(dataStream);
                    httpResponseMessage.Content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment");
                    httpResponseMessage.Content.Headers.ContentDisposition.FileName = filename;
                    httpResponseMessage.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                    return httpResponseMessage;
                }
                else
                {
                    _alert = "file not found";
                }
            }
            else
            {
                _alert = "file not found";
            }
            var obj = new { alert = _alert };
            return Request.CreateResponse(HttpStatusCode.OK, obj);
        }
        [HttpGet]
        public HttpResponseMessage Tsinfo(string id = null, string filename = null)
        {
            //var path = System.Web.HttpContext.Current.Server.MapPath("~/player_content/" + id);
            string base_content_storage = GetStoragePath();
            //string base_content_storage = ConfigurationManager.AppSettings["base_content_storage"];
            //string filename = "index.m3u8";
            var fullpath = Path.Combine(base_content_storage, id, filename + ".ts");
            string _alert = null;
            if (Directory.Exists(base_content_storage + id))
            {
                if (File.Exists(fullpath))
                {
                    var dataBytes = File.ReadAllBytes(fullpath);
                    var dataStream = new MemoryStream(dataBytes);
                    HttpResponseMessage httpResponseMessage = Request.CreateResponse(HttpStatusCode.OK);
                    httpResponseMessage.Content = new StreamContent(dataStream);
                    httpResponseMessage.Content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment");
                    httpResponseMessage.Content.Headers.ContentDisposition.FileName = filename + ".ts";
                    httpResponseMessage.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                    return httpResponseMessage;
                }
                else
                {
                    _alert = "file not found";
                }
            }
            else
            {
                _alert = "file not found";
            }
            var obj = new { alert = _alert };
            return Request.CreateResponse(HttpStatusCode.OK, obj);
        }
        [HttpGet]
        public HttpResponseMessage MediaInfo(string id = null, string fname = null)
        {
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(fname))
            {
                var obj1 = new { alert = "content id is empty!" };
                return Request.CreateResponse(HttpStatusCode.OK, obj1);
            }
            string base_content_storage = GetStoragePath();
            //string base_content_storage = ConfigurationManager.AppSettings["base_content_storage"];
            if (!Directory.Exists(base_content_storage + id))
            {
                return Request.CreateResponse(HttpStatusCode.OK, new { alert = "content id is not exist!" });
            }
            string info = RunFFM_Info(id, fname);
            string duration = getInfoPattern(info, "duration");
            string fps = getInfoPattern(info, "fps");
            string frame = "0";
            if (!string.IsNullOrEmpty(duration) && !string.IsNullOrEmpty(fps))
            {
                double _frame = 0;
                string[] ds = duration.Split(':');
                if (ds.Length == 3)
                {
                    double hour = Convert.ToDouble(ds[0]) * 60 * 60;
                    double min = Convert.ToDouble(ds[1]) * 60;
                    double sec = Convert.ToDouble(ds[2]);
                    _frame = (sec + min + hour) * Convert.ToDouble(fps);
                }
                frame = Convert.ToString(_frame);
            }

            var obj = new { duration, fps, frame };
            return Request.CreateResponse(HttpStatusCode.OK, obj);
        }
        [HttpGet]
        public HttpResponseMessage ConversionProgressInfo(string id = null)
        {
            string base_content_storage = GetStoragePath();
            //string base_content_storage = ConfigurationManager.AppSettings["base_content_storage"];
            var fullpath = Path.Combine(base_content_storage, id, "block.txt");
            string currentFrame = null;
            string status = null;
            string data = null;
            if (File.Exists(fullpath))
            {
                FileStream logFileStream = new FileStream(fullpath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                byte[] buffer;
                try
                {
                    int length = (int)logFileStream.Length;
                    buffer = new byte[length];
                    int count;
                    int sum = 0;
                    while ((count = logFileStream.Read(buffer, sum, length - sum)) > 0)
                    {
                        sum += count;
                    }
                }
                finally
                {
                    logFileStream.Close();
                }
                data = Encoding.UTF8.GetString(buffer, 0, buffer.Length);

                currentFrame = getInfoPattern(data, "progress");
                status = getInfoPattern(data, "progress_status");
            }
            var obj = new { status, currentFrame };
            return Request.CreateResponse(HttpStatusCode.OK, obj);
        }
        [HttpGet]
        public HttpResponseMessage Conversion(string id = null, string fname = null)
        {
            string _alert = null;
            StringBuilder output = new StringBuilder();
            string base_content_storage = GetStoragePath();
            //string base_content_storage = ConfigurationManager.AppSettings["base_content_storage"];
            string ext = getExtension(fname);
            string mp4_filename = ConfigurationManager.AppSettings["mp4_filename"] + "." + ext;
            var fullpath = Path.Combine(base_content_storage, id);
            if (Directory.Exists(fullpath))
            {
                string command = "ffmpeg -progress block.txt -y -i " + mp4_filename + " -hls_time 10 -hls_key_info_file enc.keyinfo -hls_playlist_type vod -hls_segment_filename \"segmentNo%d.ts\" index.m3u8";
                if (File.Exists(fullpath + "/" + mp4_filename))
                {
                    Process p = new Process();
                    p.StartInfo.FileName = "cmd.exe";
                    p.StartInfo.Arguments = "/c " + command;
                    p.StartInfo.WorkingDirectory = fullpath;
                    p.StartInfo.UseShellExecute = false;
                    p.StartInfo.CreateNoWindow = true;
                    p.StartInfo.RedirectStandardOutput = true;
                    p.StartInfo.RedirectStandardError = true;

                    p.ErrorDataReceived += new DataReceivedEventHandler((s, e) =>
                    {
                        if (e.Data != null)
                            output.Append(e.Data);
                    });

                    p.OutputDataReceived += new DataReceivedEventHandler((s, e) =>
                    {
                        if (e.Data != null)
                            output.Append(e.Data);
                    });

                    p.Start();
                    p.BeginErrorReadLine();
                    p.BeginOutputReadLine();
                    // Get the output into a string
                    //string result = p.StandardOutput.ReadToEnd();
                    //string err = p.StandardError.ReadToEnd();                   
                    p.WaitForExit();
                    _alert = output.ToString();
                    //p.Close();
                }
                else
                {
                    _alert = "File not found";
                }
            }
            var obj = new { alert = _alert };
            return Request.CreateResponse(HttpStatusCode.OK, obj);
        }

        #region IgnoreApi
        [ApiExplorerSettings(IgnoreApi = true)]
        [NonAction]
        private void DeleteDirectory(string path)
        {
            // Delete all files from the Directory  
            foreach (string filename in Directory.GetFiles(path))
            {
                File.Delete(filename);
            }
            // Check all child Directories and delete files  
            foreach (string subfolder in Directory.GetDirectories(path))
            {
                DeleteDirectory(subfolder);
            }
            Directory.Delete(path);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [NonAction]
        private void RunSSL(string name)
        {
            string base_content_storage = GetStoragePath();
            //string base_content_storage = ConfigurationManager.AppSettings["base_content_storage"];
            string path = base_content_storage + name;
            string command = "openssl rand 16 > enc.key";

            Process p = new Process();
            p.StartInfo.FileName = "cmd.exe";
            p.StartInfo.Arguments = "/c " + command;
            p.StartInfo.WorkingDirectory = path;
            p.StartInfo.UseShellExecute = false;
            // Do not create the black window.
            p.StartInfo.CreateNoWindow = true;
            // This means that it will be redirected to the Process.StandardOutput StreamReader.
            p.StartInfo.RedirectStandardOutput = true;
            p.Start();
            // Get the output into a string
            string result = p.StandardOutput.ReadToEnd();
            //p.Close();
            CreateKeyInfo(name);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [NonAction]
        private void CreateKeyInfo(string name)
        {
            string base_content_storage = GetStoragePath();
            //string base_content_storage = ConfigurationManager.AppSettings["base_content_storage"];
            string path = base_content_storage + name + @"\enc.keyinfo";
            // Check if file already exists. If yes, delete it. 
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            // Create a new file     
            using (StreamWriter sw = File.CreateText(path))
            {
                sw.WriteLine("/enc.key");
                sw.WriteLine("enc.key");
            }
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [NonAction]
        private MemoryStream M3u8DataManipulation(MemoryStream _stream, string id, string baseurl)
        {
            StreamReader dataReader = new StreamReader(_stream);
            string dataText = dataReader.ReadToEnd();
            // data Manipulation
            //string id = "20210327-181439-b9093c54-0c6e-4ae6-bdac-c70f0e9f7a95";
            string base_api_Keyinfo = string.Format("{0}api/mpeg/Keyinfo/", baseurl); // @"http://localhost:50017/api/mpeg/Keyinfo/";
            string base_api_Tsinfo = string.Format("{0}api/mpeg/Tsinfo/", baseurl); //@"http://localhost:50017/api/mpeg/Tsinfo/";

            string pattern_URI = "URI=\"(.)+\"";  // http://localhost:50017/api/mpeg/Keyinfo/20210327-181439-b9093c54-0c6e-4ae6-bdac-c70f0e9f7a95
            string newURI = "URI=\"" + base_api_Keyinfo + id + "\"";

            string pattern_segment = "(segmentNo)";
            string newSegment = base_api_Tsinfo + id + "/segmentNo";  // http://localhost:50017/api/mpeg/Tsinfo/{id}/segmentNo0

            //RegexOptions options = RegexOptions.IgnoreCase | RegexOptions.Multiline;
            dataText = Regex.Replace(dataText, pattern_URI, newURI);
            dataText = Regex.Replace(dataText, pattern_segment, newSegment);
            dataText = Regex.Replace(dataText, "(.ts)", "");

            var manipulationStream = new MemoryStream();
            var writer = new StreamWriter(manipulationStream);
            writer.Write(dataText);
            writer.Flush();
            manipulationStream.Position = 0;
            return manipulationStream;
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [NonAction]
        private string RunFFM_Info(string name, string fname)
        {
            string _alert = null;
            string base_content_storage = GetStoragePath();
            //string base_content_storage = ConfigurationManager.AppSettings["base_content_storage"];
            string mp4_filename = ConfigurationManager.AppSettings["mp4_filename"] + "." + getExtension(fname);
            string path = base_content_storage + name;
            string command = "ffmpeg -i " + mp4_filename;

            if (File.Exists(path + "/" + mp4_filename))
            {
                Process p = new Process();
                p.StartInfo.FileName = "cmd.exe";
                p.StartInfo.Arguments = "/c " + command;
                p.StartInfo.WorkingDirectory = path;
                p.StartInfo.UseShellExecute = false;
                // Do not create the black window.
                p.StartInfo.CreateNoWindow = true;
                // This means that it will be redirected to the Process.StandardOutput StreamReader.
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;
                p.Start();
                // Get the output into a string
                string result = p.StandardOutput.ReadToEnd();
                string err = p.StandardError.ReadToEnd();
                result += "\n" + err;
                p.WaitForExit();
                //p.Close();
                _alert = result;
            }
            return _alert;
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [NonAction]
        private string getInfoPattern(string str, string types)
        {
            string pattern = "";
            if ("duration" == types)
            {
                pattern = @"(?<=Duration: )[\w\:.]+";
            }
            else if ("fps" == types)
            {
                pattern = @"[\d]+(?= fps)";
            }
            else if ("progress" == types)
            {
                pattern = @"(?<=frame=)[\d]+";
            }
            else if ("progress_status" == types)
            {
                pattern = @"(?<=progress=)[\w]+";
            }
            RegexOptions options = RegexOptions.IgnoreCase | RegexOptions.Multiline;
            MatchCollection match = Regex.Matches(str, pattern, options);
            if (match.Count > 0)
            {
                if ("progress" == types || "progress_status" == types)
                {
                    str = match[match.Count - 1].Value.ToString().Trim();
                }
                else
                {
                    str = match[0].Value.ToString().Trim();
                }
            }
            return str;
        }
        [ApiExplorerSettings(IgnoreApi = true)]
        [NonAction]
        private string getExtension(string str)
        {
            string pattern = @"[^.]+$";
            string ret = "";
            RegexOptions options = RegexOptions.IgnoreCase | RegexOptions.Multiline;
            MatchCollection match = Regex.Matches(str, pattern, options);
            if (match.Count > 0)
            {
                ret = match[0].Value.ToString().Trim();
            }
            return ret;
        }

        #endregion

        #endregion

        #region Auth-JWT
        [HttpGet]
        public HttpResponseMessage AuthGetToken()
        {
            var issuer = "ffmpeg.demo.com";

            var permClaims = new List<Claim>();
            permClaims.Add(new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()));
            permClaims.Add(new Claim("username", "samratg850"));
            permClaims.Add(new Claim("role", "admin"));

            int expireMinutes = 10; // DateTime.Now.AddDays(1)

            string Secret = "db3OIsj+BXE9NZDy0t8W3TcNekrF+2d/1sFnWG4HnV8TZY30iTOdtVWJG8abWvB1GlOgJuQZdcF2Luqm/hccMw==";
            var symmetricKey = Convert.FromBase64String(Secret);
            var symmetricSecurityKey = new SymmetricSecurityKey(symmetricKey);
            var credentials = new SigningCredentials(symmetricSecurityKey, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(issuer,
                            issuer,
                            permClaims,
                            expires: DateTime.Now.AddMinutes(expireMinutes),
                            signingCredentials: credentials);
            var jwt_token = new JwtSecurityTokenHandler().WriteToken(token);
            var obj = new { alert = "AuthGetToken", token = jwt_token };
            return Request.CreateResponse(HttpStatusCode.OK, obj);
        }

        [HttpPost]
        public HttpResponseMessage AuthValidateToken()
        {
            try
            {
                HttpRequestHeaders headers = this.Request.Headers;
                string token = string.Empty;
                if (headers.Contains("token"))
                {
                    token = headers.GetValues("token").First();
                }

                string iss = null;
                string username = null;
                string role = null;
                if (!string.IsNullOrEmpty(token))
                {
                    string Secret = "db3OIsj+BXE9NZDy0t8W3TcNekrF+2d/1sFnWG4HnV8TZY30iTOdtVWJG8abWvB1GlOgJuQZdcF2Luqm/hccMw==";
                    var handler = new JwtSecurityTokenHandler();
                    var jwtToken = handler.ReadToken(token) as JwtSecurityToken;
                    if (jwtToken != null)
                    {
                        var symmetricKey = Convert.FromBase64String(Secret);
                        var validationParameters = new TokenValidationParameters()
                        {
                            RequireExpirationTime = true,
                            ValidateIssuer = false,
                            ValidateAudience = false,
                            IssuerSigningKey = new SymmetricSecurityKey(symmetricKey),
                            ClockSkew = TimeSpan.Zero
                        };
                        SecurityToken securityToken;
                        var principal = handler.ValidateToken(token, validationParameters, out securityToken);
                        if (securityToken != null)
                        {
                            username = ((JwtSecurityToken)securityToken).Payload["username"].ToString();
                            role = ((JwtSecurityToken)securityToken).Payload["role"].ToString();
                            iss = ((JwtSecurityToken)securityToken).Payload["iss"].ToString();
                        }
                        return Request.CreateResponse(HttpStatusCode.OK, new { username, role, iss });
                    }
                }
                return Request.CreateResponse(HttpStatusCode.OK, new { alert = "Token Not Found" });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.OK, new { alert = TokenException(ex.ToString()), error = ex.ToString() });
            }
        }

        [NonAction]
        private string TokenException(string error)
        {
            string ret = "Invalid token!";
            string pattern = @"(Unable to decode the header)|(Unable to decode the payload)|(Signature validation failed)|(The token is expired)";
            RegexOptions options = RegexOptions.IgnoreCase | RegexOptions.Multiline;
            MatchCollection match = Regex.Matches(error, pattern, options);
            if (match.Count > 0)
            {
                ret = match[0].Value.ToString().Trim() + "!";
            }
            return ret;
        }

        #endregion

        #region UI
        [HttpGet]
        public HttpResponseMessage getClasses()
        {
            //string FFMpegCon = ConfigurationManager.ConnectionStrings["FFMpeg"].ConnectionString;
            //SqlConnection con = new SqlConnection(FFMpegCon);
            #region SQlite database
            string FFMpegCon = GetSQLiteConnection();
            if (string.IsNullOrEmpty(FFMpegCon))
            {
                return Request.CreateResponse(HttpStatusCode.NotFound, new { data = "no database found!" });
            }
            SQLiteConnection con = new SQLiteConnection(FFMpegCon);
            #endregion
            string sql = @"SELECT * FROM [tblClass] order by id";
            List<GetClassNames> my_class = con.Query<GetClassNames>(sql).ToList<GetClassNames>();
            var obj = new { data = my_class };
            return Request.CreateResponse(HttpStatusCode.OK, obj);
        }
        [HttpGet]
        public HttpResponseMessage getSubject()
        {
            //string FFMpegCon = ConfigurationManager.ConnectionStrings["FFMpeg"].ConnectionString;
            //SqlConnection con = new SqlConnection(FFMpegCon);
            #region SQlite database
            string FFMpegCon = GetSQLiteConnection();
            if (string.IsNullOrEmpty(FFMpegCon))
            {
                return Request.CreateResponse(HttpStatusCode.NotFound, new { data = "no database found!" });
            }
            SQLiteConnection con = new SQLiteConnection(FFMpegCon);
            #endregion
            string sql = @"SELECT * FROM [tblSubject] order by id";
            List<GetSubjectNames> my_subject = con.Query<GetSubjectNames>(sql).ToList<GetSubjectNames>();
            var obj = new { data = my_subject };
            return Request.CreateResponse(HttpStatusCode.OK, obj);
        }
        [HttpGet]
        public HttpResponseMessage getChapter(string cls = null, string sub = null)
        {
            var objAlert = new { data = "Please Provide Class and Subject Id" };
            if (!string.IsNullOrEmpty(cls) && !string.IsNullOrEmpty(sub))
            {
                //string FFMpegCon = ConfigurationManager.ConnectionStrings["FFMpeg"].ConnectionString;
                //SqlConnection con = new SqlConnection(FFMpegCon);
                #region SQlite database
                string FFMpegCon = GetSQLiteConnection();
                if (string.IsNullOrEmpty(FFMpegCon))
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound, new { data = "no database found!" });
                }
                SQLiteConnection con = new SQLiteConnection(FFMpegCon);
                #endregion
                var parameters = new { cls, sub };
                string sql = @"SELECT * FROM [tblChapter] WHERE classId = @cls AND subjectId = @sub order by id";
                List<GetChapters> my_chapter = con.Query<GetChapters>(sql, parameters).ToList<GetChapters>();
                var obj = new { data = my_chapter };
                return Request.CreateResponse(HttpStatusCode.OK, obj);
            }
            else
            {
                return Request.CreateResponse(HttpStatusCode.OK, objAlert);
            }
        }
        #endregion

        #region UI Admin
        [HttpPost]
        public HttpResponseMessage UploadFile(dynamic data)
        {
            /* Info
             * IsConversion 0 mean just uploaded
             * IsConversion 1 mean just conversion started
             * IsConversion 2 mean conversion progress finished             
             */
            //string base_content_storage = ConfigurationManager.AppSettings["base_content_storage"];
            string base_content_storage = GetStoragePath();
            HttpResponseMessage result = null;
            var obj = new { data = "" };
            var httpRequest = HttpContext.Current.Request;
            if (httpRequest.Files.Count > 0)
            {
                var uid = httpRequest.Params.GetValues("uid")[0];
                string mp4_filename = ConfigurationManager.AppSettings["mp4_filename"];
                var file_name = httpRequest.Params.GetValues("file_name")[0];
                var file_title = httpRequest.Params.GetValues("file_title")[0];
                var postedFile = httpRequest.Files[0];
                string ext = getExtension(file_name);
                CreateFolder(uid);
                //var filePath = HttpContext.Current.Server.MapPath("~/App_Data/" + postedFile.FileName);
                var filePath = Path.Combine(base_content_storage, uid, mp4_filename + "." + ext);
                postedFile.SaveAs(filePath);
                //string FFMpegCon = ConfigurationManager.ConnectionStrings["FFMpeg"].ConnectionString;
                //SqlConnection con = new SqlConnection(FFMpegCon);
                #region SQlite database
                string FFMpegCon = GetSQLiteConnection();
                if (string.IsNullOrEmpty(FFMpegCon))
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound, new { data = "no database found!" });
                }
                SQLiteConnection con = new SQLiteConnection(FFMpegCon);
                #endregion
                string sql = @"INSERT INTO tblContent([contentID],[contentTitle],[contentFileName],[IsConversion])
                VALUES (@contentID,@contentTitle,@contentFileName,@IsConversion)";
                var insert_result = con.Execute(sql,
                    new
                    {
                        @contentID = uid,
                        @contentTitle = file_title,
                        @contentFileName = file_name,
                        @IsConversion = "0"
                    });
                var obj2 = new { data = "File uploaded successfully", result = Convert.ToString(insert_result) };
                result = Request.CreateResponse(HttpStatusCode.Created, obj2);
            }
            else
            {
                obj = new { data = "Error Occured!" };
                result = Request.CreateResponse(HttpStatusCode.BadRequest);
            }
            return result;
        }
        [HttpGet]
        public HttpResponseMessage getContent(string _id = null)
        {
            //string FFMpegCon = ConfigurationManager.ConnectionStrings["FFMpeg"].ConnectionString;
            //SqlConnection con = new SqlConnection(FFMpegCon);
            #region SQlite database
            string FFMpegCon = GetSQLiteConnection();
            if (string.IsNullOrEmpty(FFMpegCon))
            {
                return Request.CreateResponse(HttpStatusCode.NotFound, new { data = "no database found!" });
            }
            SQLiteConnection con = new SQLiteConnection(FFMpegCon);
            #endregion
            string sql = @"SELECT con.* FROM [tblContent] con
                        LEFT JOIN [tblChapter] ch
                        ON con.contentID = ch.contentID";
            if (string.IsNullOrEmpty(_id))
            {
                sql += " WHERE ch.contentID is null";
            }
            else
            {
                sql += " WHERE ch.contentID is null OR ch.contentID = '" + _id + "'";
            }
            List<GetContents> data = con.Query<GetContents>(sql).ToList<GetContents>();
            var obj = new { data };
            return Request.CreateResponse(HttpStatusCode.OK, obj);
        }
        [HttpPost]
        public HttpResponseMessage RestoreKey2SD(CreateKeyBody createKeyBody)
        {
            if (!String.IsNullOrWhiteSpace(createKeyBody.Id))
            {
                //string base_content_storage = ConfigurationManager.AppSettings["base_content_storage"];
                string base_content_storage = GetStoragePath();
                string path = base_content_storage + createKeyBody.Id;
                string keyFile = Path.Combine(path, "enc.key");
                string OpenKey = null;
                if (Directory.Exists(path))
                {
                    //get key from db
                    //string FFMpegCon = ConfigurationManager.ConnectionStrings["FFMpeg"].ConnectionString;
                    //SqlConnection con = new SqlConnection(FFMpegCon);
                    #region SQlite database
                    string FFMpegCon = GetSQLiteConnection();
                    if (string.IsNullOrEmpty(FFMpegCon))
                    {
                        return Request.CreateResponse(HttpStatusCode.NotFound, new { data = "no database found!" });
                    }
                    SQLiteConnection con = new SQLiteConnection(FFMpegCon);
                    #endregion
                    OpenKey = con.ExecuteScalar<string>("SELECT OpenKey FROM tblContent WHERE contentID = @contentID", new
                    {
                        @contentID = createKeyBody.Id
                    });
                    if (!File.Exists(keyFile))
                    {
                        using (FileStream fs = File.Create(keyFile))
                        {
                            byte[] OpenKeyB = Encoding.Default.GetBytes(OpenKey);
                            fs.Write(OpenKeyB, 0, OpenKeyB.Length);
                        }
                    }
                }
                return Request.CreateResponse(HttpStatusCode.OK, new { data = "Key restore to device" });
            }
            else
            {
                return Request.CreateResponse(HttpStatusCode.OK, new { data = "Please provide content id" });
            }
        }
        [HttpPost]
        public HttpResponseMessage RemoveKeyFromSD(CreateKeyBody createKeyBody)
        {
            if (!String.IsNullOrWhiteSpace(createKeyBody.Id))
            {
                //string base_content_storage = ConfigurationManager.AppSettings["base_content_storage"];
                string base_content_storage = GetStoragePath();
                string KeyFile = Path.Combine(base_content_storage, createKeyBody.Id, "enc.key");
                if (File.Exists(KeyFile))
                {
                    File.Delete(KeyFile);
                }
                return Request.CreateResponse(HttpStatusCode.OK, new { data = "Key remove from device" });
            }
            else
            {
                return Request.CreateResponse(HttpStatusCode.OK, new { data = "Please provide content id" });
            }
        }
        [HttpPost]
        public HttpResponseMessage ConversionEnded(CreateKeyBody createKeyBody)
        {
            if (!String.IsNullOrWhiteSpace(createKeyBody.Id))
            {
                //string FFMpegCon = ConfigurationManager.ConnectionStrings["FFMpeg"].ConnectionString;
                //SqlConnection con = new SqlConnection(FFMpegCon);
                #region SQlite database
                string FFMpegCon = GetSQLiteConnection();
                if (string.IsNullOrEmpty(FFMpegCon))
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound, new { data = "no database found!" });
                }
                SQLiteConnection con = new SQLiteConnection(FFMpegCon);
                #endregion
                string sql = @"UPDATE tblContent SET [IsConversion]=@IsConversion WHERE [contentID]=@contentID";
                var update_result = con.Execute(sql,
                        new
                        {
                            @contentID = createKeyBody.Id,
                            @IsConversion = "2"
                        });
                DeleteRawMp4(createKeyBody);
                RemoveKeyFromSD(createKeyBody);
                return Request.CreateResponse(HttpStatusCode.OK, new { data = "Task after end of conversion has been completed" });
            }
            else
            {
                return Request.CreateResponse(HttpStatusCode.OK, new { data = "Please provide content id" });
            }
        }
        [HttpGet]
        public HttpResponseMessage getContentPage([FromUri] GetContentPage getContentPage)
        {
            NameValueCollection nvc = HttpUtility.ParseQueryString(Request.RequestUri.Query);
            string pageindex = getContentPage.pageindex;
            string limit = getContentPage.limit;
            string orderby = getContentPage.orderby;
            string desc = getContentPage.desc; // 'true'|'false'
            string contentID = getContentPage.contentID;
            string contentFileName = getContentPage.contentFileName;
            string contentTitle = getContentPage.contentTitle;

            #region Constant           
            if (orderby == null)
            {
                orderby = "contentID";
            }
            if (desc == "false")
            {
                desc = "asc";
            }
            else if (desc == "true" || desc == null)
            {
                desc = "desc";
            }
            string _where = "";
            if (contentFileName != null)
            {
                _where = " WHERE contentFileName like '%" + contentFileName + "%'";
            }
            else if (contentID != null)
            {
                _where = " WHERE contentID like '%" + contentID + "%'";
            }
            else if (contentTitle != null)
            {
                _where = " WHERE contentTitle like '%" + contentTitle + "%'";
            }

            int _limit = 3;
            int _pageindex = 0;
            if (limit != null)
            {
                Int32.TryParse(limit, out _limit);
                if (_limit < 3)
                    _limit = 3;
            }
            if (pageindex != null)
            {
                Int32.TryParse(pageindex, out _pageindex);
                if (_pageindex < 0)
                    _pageindex = 0;
            }
            int _offset = _pageindex * _limit;

            #endregion
            //string FFMpegCon = ConfigurationManager.ConnectionStrings["FFMpeg"].ConnectionString;
            //SqlConnection con = new SqlConnection(FFMpegCon);
            #region SQlite database
            string FFMpegCon = GetSQLiteConnection();
            if (string.IsNullOrEmpty(FFMpegCon))
            {
                return Request.CreateResponse(HttpStatusCode.NotFound, new { data = "no database found!" });
            }
            SQLiteConnection con = new SQLiteConnection(FFMpegCon);
            #endregion
            //string sql = @"SELECT * FROM [tblContent]" + _where + " order by " + orderby + " " + desc + " OFFSET " + _offset + " rows FETCH NEXT " + _limit + " rows only";
            string sql = @"SELECT * FROM [tblContent]" + _where + " order by " + orderby + " " + desc + " LIMIT " + _limit + " OFFSET " + _offset;
            List<GetContents> data = con.Query<GetContents>(sql).ToList<GetContents>();
            string count = con.ExecuteScalar<string>("SELECT count(*) FROM tblContent" + _where);

            int _count = data.Count();
            Int32.TryParse(count, out _count);
            int _totalPage = Convert.ToInt32(_count / _limit);
            if ((_count % _limit) > 0)
            {
                _totalPage += 1;
            }

            var obj = new { data, pageindex = Convert.ToString(_pageindex), totalPage = Convert.ToString(_totalPage) };
            return Request.CreateResponse(HttpStatusCode.OK, obj);
        }
        [HttpPost]
        public HttpResponseMessage Deletecontent(CreateKeyBody createKeyBody)
        {
            if (!String.IsNullOrWhiteSpace(createKeyBody.Id))
            {
                //string base_content_storage = ConfigurationManager.AppSettings["base_content_storage"];
                string base_content_storage = GetStoragePath();
                string path = base_content_storage + createKeyBody.Id;
                //string FFMpegCon = ConfigurationManager.ConnectionStrings["FFMpeg"].ConnectionString;
                //SqlConnection con = new SqlConnection(FFMpegCon);
                #region SQlite database
                string FFMpegCon = GetSQLiteConnection();
                if (string.IsNullOrEmpty(FFMpegCon))
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound, new { data = "no database found!" });
                }
                SQLiteConnection con = new SQLiteConnection(FFMpegCon);
                #endregion
                string sql = @"DELETE FROM tblContent WHERE [contentID]=@contentID;
                             UPDATE tblChapter SET [contentID]=null WHERE [contentID]=@contentID";
                var delete_result = con.Execute(sql,
                    new
                    {
                        @contentid = createKeyBody.Id
                    });
                string _alert = "";
                if (delete_result > 0)
                {
                    _alert = "ContentID deleted from db. ";
                }
                var res = DeleteFolder(createKeyBody.Id).Content.ReadAsStringAsync().Result;
                var jsonString = JsonConvert.DeserializeObject<Dictionary<string, string>>(res);
                _alert += jsonString["alert"].ToString();
                return Request.CreateResponse(HttpStatusCode.OK, new { data = _alert });
            }
            else
            {
                return Request.CreateResponse(HttpStatusCode.OK, new { data = "Please provide content id" });
            }
        }

        [HttpPost]
        public HttpResponseMessage AddSubject(SetSubject setSubject)
        {
            if (!String.IsNullOrEmpty(setSubject.Name))
            {
                //string FFMpegCon = ConfigurationManager.ConnectionStrings["FFMpeg"].ConnectionString;
                //SqlConnection con = new SqlConnection(FFMpegCon);
                #region SQlite database
                string FFMpegCon = GetSQLiteConnection();
                if (string.IsNullOrEmpty(FFMpegCon))
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound, new { data = "no database found!" });
                }
                SQLiteConnection con = new SQLiteConnection(FFMpegCon);
                #endregion
                string sql = @"INSERT INTO tblSubject([subjectName]) VALUES (@subjectName)";
                var insert_result = con.Execute(sql,
                    new
                    {
                        @subjectName = setSubject.Name
                    });
                return Request.CreateResponse(HttpStatusCode.Created, new { data = "data saved successfully" });
            }
            else
            {
                return Request.CreateResponse(HttpStatusCode.OK, new { data = "Please provide subject name" });
            }
        }
        [HttpPost]
        public HttpResponseMessage RemoveSubject(SetSubject setSubject)
        {
            if (!String.IsNullOrEmpty(setSubject.Id))
            {
                //string FFMpegCon = ConfigurationManager.ConnectionStrings["FFMpeg"].ConnectionString;
                //SqlConnection con = new SqlConnection(FFMpegCon);
                #region SQlite database
                string FFMpegCon = GetSQLiteConnection();
                if (string.IsNullOrEmpty(FFMpegCon))
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound, new { data = "no database found!" });
                }
                SQLiteConnection con = new SQLiteConnection(FFMpegCon);
                #endregion
                //string sql = @"DELETE FROM tblSubject WHERE [id] = @Id;
                //               DBCC CHECKIDENT([tblSubject], RESEED, 0);
                //               DBCC CHECKIDENT([tblSubject]);";
                string sql = @"DELETE FROM tblSubject WHERE [id] = @Id;
                               delete from SQLITE_SEQUENCE where name ='tblSubject'";
                var insert_result = con.Execute(sql,
                    new
                    {
                        @Id = setSubject.Id
                    });
                return Request.CreateResponse(HttpStatusCode.Created, new { data = "data removed successfully" });
            }
            else
            {
                return Request.CreateResponse(HttpStatusCode.OK, new { data = "Please provide subject id" });
            }
        }
        [HttpPost]
        public HttpResponseMessage PutSubject(SetSubject setSubject)
        {
            if (!String.IsNullOrEmpty(setSubject.Id) && !String.IsNullOrEmpty(setSubject.Name))
            {
                //string FFMpegCon = ConfigurationManager.ConnectionStrings["FFMpeg"].ConnectionString;
                //SqlConnection con = new SqlConnection(FFMpegCon);
                #region SQlite database
                string FFMpegCon = GetSQLiteConnection();
                if (string.IsNullOrEmpty(FFMpegCon))
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound, new { data = "no database found!" });
                }
                SQLiteConnection con = new SQLiteConnection(FFMpegCon);
                #endregion
                string sql = @"UPDATE tblSubject SET [subjectName]=@subjectName WHERE [id] = @Id";
                var insert_result = con.Execute(sql,
                    new
                    {
                        @Id = setSubject.Id,
                        @subjectName = setSubject.Name
                    });
                return Request.CreateResponse(HttpStatusCode.Created, new { data = "data saved successfully" });
            }
            else
            {
                return Request.CreateResponse(HttpStatusCode.OK, new { data = "Please provide subject id and name" });
            }
        }

        [HttpPost]
        public HttpResponseMessage AddClass(SetSubject setSubject)
        {
            if (!String.IsNullOrEmpty(setSubject.Name))
            {
                //string FFMpegCon = ConfigurationManager.ConnectionStrings["FFMpeg"].ConnectionString;
                //SqlConnection con = new SqlConnection(FFMpegCon);
                #region SQlite database
                string FFMpegCon = GetSQLiteConnection();
                if (string.IsNullOrEmpty(FFMpegCon))
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound, new { data = "no database found!" });
                }
                SQLiteConnection con = new SQLiteConnection(FFMpegCon);
                #endregion
                string sql = @"INSERT INTO tblClass([className]) VALUES (@className)";
                var insert_result = con.Execute(sql,
                    new
                    {
                        @className = setSubject.Name
                    });
                return Request.CreateResponse(HttpStatusCode.Created, new { data = "data saved successfully" });
            }
            else
            {
                return Request.CreateResponse(HttpStatusCode.OK, new { data = "Please provide class name" });
            }
        }
        [HttpPost]
        public HttpResponseMessage RemoveClass(SetSubject setSubject)
        {
            if (!String.IsNullOrEmpty(setSubject.Id))
            {
                //string FFMpegCon = ConfigurationManager.ConnectionStrings["FFMpeg"].ConnectionString;
                //SqlConnection con = new SqlConnection(FFMpegCon);
                #region SQlite database
                string FFMpegCon = GetSQLiteConnection();
                if (string.IsNullOrEmpty(FFMpegCon))
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound, new { data = "no database found!" });
                }
                SQLiteConnection con = new SQLiteConnection(FFMpegCon);
                #endregion
                //string sql = @"DELETE FROM tblClass WHERE [id] = @Id;
                //               DBCC CHECKIDENT([tblClass], RESEED, 0);
                //               DBCC CHECKIDENT([tblClass]);";
                string sql = @"DELETE FROM tblClass WHERE [id] = @Id;
                               delete from SQLITE_SEQUENCE where name ='tblClass'";
                var insert_result = con.Execute(sql,
                    new
                    {
                        @Id = setSubject.Id
                    });
                return Request.CreateResponse(HttpStatusCode.Created, new { data = "data removed successfully" });
            }
            else
            {
                return Request.CreateResponse(HttpStatusCode.OK, new { data = "Please provide class id" });
            }
        }
        [HttpPost]
        public HttpResponseMessage PutClass(SetSubject setSubject)
        {
            if (!String.IsNullOrEmpty(setSubject.Id) && !String.IsNullOrEmpty(setSubject.Name))
            {
                //string FFMpegCon = ConfigurationManager.ConnectionStrings["FFMpeg"].ConnectionString;
                //SqlConnection con = new SqlConnection(FFMpegCon);
                #region SQlite database
                string FFMpegCon = GetSQLiteConnection();
                if (string.IsNullOrEmpty(FFMpegCon))
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound, new { data = "no database found!" });
                }
                SQLiteConnection con = new SQLiteConnection(FFMpegCon);
                #endregion
                string sql = @"UPDATE tblClass SET [className]=@className WHERE [id] = @Id";
                var insert_result = con.Execute(sql,
                    new
                    {
                        @Id = setSubject.Id,
                        @className = setSubject.Name
                    });
                return Request.CreateResponse(HttpStatusCode.Created, new { data = "data saved successfully" });
            }
            else
            {
                return Request.CreateResponse(HttpStatusCode.OK, new { data = "Please provide class id and name" });
            }
        }

        [HttpPost]
        public HttpResponseMessage AddChapter(GetChapters getChapters)
        {
            string _alert = "";
            //if (String.IsNullOrEmpty(getChapters.contentID))
            //{
            //    _alert = "contentID is empty!";
            //}
            if (String.IsNullOrEmpty(getChapters.chapterName))
            {
                _alert = "chapterName is empty!";
            }
            else if (String.IsNullOrEmpty(getChapters.subjectId))
            {
                _alert = "subjectId is empty!";
            }
            else if (String.IsNullOrEmpty(getChapters.classId))
            {
                _alert = "classId is empty!";
            }
            else
            {
                //string FFMpegCon = ConfigurationManager.ConnectionStrings["FFMpeg"].ConnectionString;
                //SqlConnection con = new SqlConnection(FFMpegCon);
                #region SQlite database
                string FFMpegCon = GetSQLiteConnection();
                if (string.IsNullOrEmpty(FFMpegCon))
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound, new { data = "no database found!" });
                }
                SQLiteConnection con = new SQLiteConnection(FFMpegCon);
                #endregion
                string sql = @"INSERT INTO tblChapter 
                              ([chapterName],[subjectId],[classId],[contentID])      
                              VALUES(@chapterName,@subjectId,@classId,@contentID)";
                var insert_result = con.Execute(sql,
                    new
                    {
                        @contentID = getChapters.contentID,
                        @chapterName = getChapters.chapterName,
                        @subjectId = getChapters.subjectId,
                        @classId = getChapters.classId
                    });
                _alert = "data saved successfully!";
                return Request.CreateResponse(HttpStatusCode.Created, new { data = _alert });
            }
            return Request.CreateResponse(HttpStatusCode.OK, new { data = _alert });
        }
        [HttpPost]
        public HttpResponseMessage RemoveChapter(GetChapters getChapters)
        {
            if (!String.IsNullOrEmpty(getChapters.id))
            {
                //string FFMpegCon = ConfigurationManager.ConnectionStrings["FFMpeg"].ConnectionString;
                //SqlConnection con = new SqlConnection(FFMpegCon);
                #region SQlite database
                string FFMpegCon = GetSQLiteConnection();
                if (string.IsNullOrEmpty(FFMpegCon))
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound, new { data = "no database found!" });
                }
                SQLiteConnection con = new SQLiteConnection(FFMpegCon);
                #endregion
                //string sql = @"DELETE FROM tblChapter WHERE [id] = @Id;
                //               DBCC CHECKIDENT([tblChapter], RESEED, 0);
                //               DBCC CHECKIDENT([tblChapter]);";      
                string sql = @"DELETE FROM tblChapter WHERE [id] = @Id;
                               delete from SQLITE_SEQUENCE where name ='tblChapter'";
                var insert_result = con.Execute(sql,
                    new
                    {
                        @Id = getChapters.id
                    });
                return Request.CreateResponse(HttpStatusCode.Created, new { data = "data removed successfully" });
            }
            else
            {
                return Request.CreateResponse(HttpStatusCode.OK, new { data = "Please provide chapter id" });
            }
        }
        [HttpPost]
        public HttpResponseMessage PutChapter(GetChapters getChapters)
        {
            string _alert = "";
            //if (!String.IsNullOrEmpty(getChapters.contentID))
            //{
            //    _alert = "contentID is empty!";
            //}
            if (String.IsNullOrEmpty(getChapters.id))
            {
                _alert = "ChapterID is empty!";
            }
            else if (String.IsNullOrEmpty(getChapters.chapterName))
            {
                _alert = "chapterName is empty!";
            }
            else if (String.IsNullOrEmpty(getChapters.subjectId))
            {
                _alert = "subjectId is empty!";
            }
            else if (String.IsNullOrEmpty(getChapters.classId))
            {
                _alert = "classId is empty!";
            }
            else
            {
                //string FFMpegCon = ConfigurationManager.ConnectionStrings["FFMpeg"].ConnectionString;
                //SqlConnection con = new SqlConnection(FFMpegCon);
                #region SQlite database
                string FFMpegCon = GetSQLiteConnection();
                if (string.IsNullOrEmpty(FFMpegCon))
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound, new { data = "no database found!" });
                }
                SQLiteConnection con = new SQLiteConnection(FFMpegCon);
                #endregion
                string sql = @"UPDATE tblChapter SET [contentID]=@contentID,chapterName=@chapterName,subjectId=@subjectId,classId=@classId
                              WHERE [id] = @Id";
                var insert_result = con.Execute(sql,
                    new
                    {
                        @contentID = getChapters.contentID,
                        @chapterName = getChapters.chapterName,
                        @subjectId = getChapters.subjectId,
                        @classId = getChapters.classId,
                        @Id = getChapters.id,
                    });
                _alert = "data updated successfully!";
                return Request.CreateResponse(HttpStatusCode.Created, new { data = _alert });
            }
            return Request.CreateResponse(HttpStatusCode.OK, new { data = _alert });
        }
        [HttpGet]
        public HttpResponseMessage getChapterFilter([FromUri] GetChapterPage getChapterPage)
        {
            string pageindex = getChapterPage.pageindex;
            string limit = getChapterPage.limit;
            string orderby = getChapterPage.orderby;
            string desc = getChapterPage.desc; // 'true'|'false'

            string chapterName = getChapterPage.chapterName;
            string contentID = getChapterPage.contentID;
            string subjectName = getChapterPage.subjectName;
            string className = getChapterPage.className;

            #region Constant           
            if (orderby == null)
            {
                orderby = "chapterName";
            }
            if (desc == "false" || desc == null)
            {
                desc = "asc";
            }
            else if (desc == "true")
            {
                desc = "desc";
            }
            string _where = "";
            if (chapterName != null)
            {
                //_where = " WHERE chapterName like N'%" + chapterName + "%'";
                _where = " WHERE chapterName like '%" + chapterName + "%'";
            }
            if (contentID != null)
            {
                if (_where == "")
                    _where = " WHERE";
                else
                    _where += " AND";
                _where += " contentID like '%" + contentID + "%'";
            }
            if (subjectName != null)
            {
                if (_where == "")
                    _where = " WHERE";
                else
                    _where += " AND";
                _where += " sub.subjectName like '%" + subjectName + "%'";
            }
            if (className != null)
            {
                if (_where == "")
                    _where = " WHERE";
                else
                    _where += " AND";
                //_where += " cls.className = '" + className + "'";
                _where += " cls.className = '" + className + "' COLLATE NOCASE";
            }

            int _limit = 3;
            int _pageindex = 0;
            if (limit != null)
            {
                Int32.TryParse(limit, out _limit);
                if (_limit < 3)
                    _limit = 3;
            }
            if (pageindex != null)
            {
                Int32.TryParse(pageindex, out _pageindex);
                if (_pageindex < 0)
                    _pageindex = 0;
            }
            int _offset = _pageindex * _limit;

            #endregion
            //string FFMpegCon = ConfigurationManager.ConnectionStrings["FFMpeg"].ConnectionString;
            //SqlConnection con = new SqlConnection(FFMpegCon);
            #region SQlite database
            string FFMpegCon = GetSQLiteConnection();
            if (string.IsNullOrEmpty(FFMpegCon))
            {
                return Request.CreateResponse(HttpStatusCode.NotFound, new { data = "no database found!" });
            }
            SQLiteConnection con = new SQLiteConnection(FFMpegCon);
            #endregion
            //string _from = @" FROM [dbo].[tblChapter] ch
            //            LEFT JOIN [dbo].[tblSubject] sub
            //            ON ch.subjectId = sub.id
            //            LEFT JOIN [dbo].[tblClass] cls
            //            ON ch.classId = cls.id";
            string _from = @" FROM [tblChapter] ch
                        LEFT JOIN [tblSubject] sub
                        ON ch.subjectId = sub.id
                        LEFT JOIN [tblClass] cls
                        ON ch.classId = cls.id";
            //string sql = @"SELECT ch.*, sub.subjectName, cls.className" + _from + _where + " order by " + orderby + " " + desc + " OFFSET " + _offset + " rows FETCH NEXT " + _limit + " rows only";
            string sql = @"SELECT ch.*, sub.subjectName, cls.className" + _from + _where + " order by " + orderby + " " + desc + " LIMIT " + _limit + " OFFSET " + _offset;
            List<GetChapters> data = con.Query<GetChapters>(sql).ToList<GetChapters>();
            string count = con.ExecuteScalar<string>("SELECT count(*)" + _from + _where);

            int _count = data.Count();
            Int32.TryParse(count, out _count);
            int _totalPage = Convert.ToInt32(_count / _limit);
            if ((_count % _limit) > 0)
            {
                _totalPage += 1;
            }

            var obj = new { data, pageindex = Convert.ToString(_pageindex), totalPage = Convert.ToString(_totalPage) };
            return Request.CreateResponse(HttpStatusCode.OK, obj);
        }

        #region IgnoreApi
        [ApiExplorerSettings(IgnoreApi = true)]
        [NonAction]
        private void Key2DB(string contentId)
        {
            string base_content_storage = GetStoragePath();
            //string base_content_storage = ConfigurationManager.AppSettings["base_content_storage"];
            var fullpath = Path.Combine(base_content_storage, contentId, "enc.key");
            if (Directory.Exists(base_content_storage + contentId))
            {
                if (File.Exists(fullpath))
                {
                    var dataBytes = File.ReadAllBytes(fullpath);
                    string OpenKey = Encoding.Default.GetString(dataBytes);
                    //string FFMpegCon = ConfigurationManager.ConnectionStrings["FFMpeg"].ConnectionString;
                    //SqlConnection con = new SqlConnection(FFMpegCon);
                    #region SQlite database
                    string FFMpegCon = GetSQLiteConnection();
                    SQLiteConnection con = new SQLiteConnection(FFMpegCon);
                    #endregion
                    string sql = @"UPDATE tblContent SET [IsConversion]=@IsConversion,[OpenKey]=@OpenKey
                           WHERE [contentID]=@contentID";
                    var update_result = con.Execute(sql,
                        new
                        {
                            @contentID = contentId,
                            @IsConversion = "1",
                            @OpenKey = OpenKey
                        });
                }
            }
        }
        [NonAction]
        private void DeleteRawMp4(CreateKeyBody createKeyBody)
        {
            //string FFMpegCon = ConfigurationManager.ConnectionStrings["FFMpeg"].ConnectionString;
            //SqlConnection con = new SqlConnection(FFMpegCon);
            #region SQlite database
            string FFMpegCon = GetSQLiteConnection();
            //if (string.IsNullOrEmpty(FFMpegCon))
            //{
            //    return Request.CreateResponse(HttpStatusCode.NotFound, new { data = "no database found!" });
            //}
            SQLiteConnection con = new SQLiteConnection(FFMpegCon);
            #endregion
            string sql = @"SELECT * FROM tblContent WHERE [contentID]=@contentID";
            List<GetContents> data = con.Query<GetContents>(sql,
                    new
                    {
                        @contentID = createKeyBody.Id,
                        @IsConversion = "2"
                    }).ToList<GetContents>();
            if (data.Count() > 0)
            {
                string base_content_storage = GetStoragePath();
                //string base_content_storage = ConfigurationManager.AppSettings["base_content_storage"];
                string mp4_filename = ConfigurationManager.AppSettings["mp4_filename"];
                string filename = data[0].contentFileName;
                string ext = getExtension(filename);
                var filePath = Path.Combine(base_content_storage, createKeyBody.Id, mp4_filename + "." + ext);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
        }
        [NonAction]
        public string GetStoragePath()
        {
            string base_directory_name = ConfigurationManager.AppSettings["base_content_storage_project_root_directory_name"];
            var filePath = HttpContext.Current.Server.MapPath("~/" + base_directory_name + "/");

            string external_content_storage = ConfigurationManager.AppSettings["external_content_storage"];
            string base_content_storage = ConfigurationManager.AppSettings["base_content_storage"];
            if ("true" == external_content_storage)
            {
                filePath = base_content_storage;
            }

            return filePath;
        }
        #endregion

        #endregion

        #region SQlite
        [NonAction]
        public string GetSQLiteConnection()
        {
            string _password = "sqlite_A1d4m1N";
            #region HavePass
            bool HavePass = false;
            string sqlite_infodb_name = "Info.db";
            var sqlite_infodb = HttpContext.Current.Server.MapPath("~/App_Data/" + sqlite_infodb_name);
            string conStr = "";
            if (File.Exists(sqlite_infodb))
            {
                string conStrInfoDB = string.Format("Data Source={0};Version=3;", sqlite_infodb);
                SQLiteConnection con = new SQLiteConnection(conStrInfoDB);
                string sql = @"select * from havePass LIMIT 1";
                string _havePass = con.ExecuteScalar<string>(sql);
                HavePass = _havePass == "0" ? false : true;
            }
            #endregion

            string sqlite_db_name = ConfigurationManager.AppSettings["sqlite_db_name"];
            var sqlite_db = HttpContext.Current.Server.MapPath("~/App_Data/" + sqlite_db_name);
            if (File.Exists(sqlite_db))
            {
                conStr = string.Format("Data Source={0};Version=3;", sqlite_db);
                if (HavePass)
                {
                    conStr = string.Format("Data Source={0};Version=3;Password={1};", sqlite_db, _password);
                }
            }
            return conStr;
        }
        [NonAction]
        public void UpdateHavePassword(int havePass)
        {
            string sqlite_infodb_name = "Info.db";
            var sqlite_infodb = HttpContext.Current.Server.MapPath("~/App_Data/" + sqlite_infodb_name);
            if (File.Exists(sqlite_infodb))
            {
                string conStrInfoDB = string.Format("Data Source={0};Version=3;", sqlite_infodb);
                SQLiteConnection conInfo = new SQLiteConnection(conStrInfoDB);
                string sql = @"UPDATE havePass SET havePass = @havePass;";
                var insert_result = conInfo.Execute(sql,
                    new
                    {
                        @havePass = havePass
                    });
            }
        }
        [NonAction]
        public string GetSQLiteConnectionChangePassword(string _password = "")
        {
            string sqlite_db_name = ConfigurationManager.AppSettings["sqlite_db_name"];
            var sqlite_db = HttpContext.Current.Server.MapPath("~/App_Data/" + sqlite_db_name);
            string conStr = "";
            if (File.Exists(sqlite_db))
            {
                conStr = string.Format("Data Source={0};Version=3;", sqlite_db);
                if (!string.IsNullOrEmpty(_password))
                {
                    conStr = string.Format("Data Source={0};Version=3;Password={1};", sqlite_db, _password);
                }
            }
            return conStr;
        }
        [HttpGet]
        public HttpResponseMessage SQliteHavePassword()
        {
            #region HavePass
            bool HavePass = false;
            string sqlite_infodb_name = "Info.db";
            var sqlite_infodb = HttpContext.Current.Server.MapPath("~/App_Data/" + sqlite_infodb_name);
            if (File.Exists(sqlite_infodb))
            {
                string conStrInfoDB = string.Format("Data Source={0};Version=3;", sqlite_infodb);
                SQLiteConnection con = new SQLiteConnection(conStrInfoDB);
                string sql = @"select * from havePass LIMIT 1";
                string _havePass = con.ExecuteScalar<string>(sql);
                HavePass = _havePass == "0" ? false : true;
            }
            return Request.CreateResponse(HttpStatusCode.OK, new { data = HavePass });
            #endregion
        }
        [HttpGet]
        public HttpResponseMessage SQliteChangePassword(string secret = null, string type = "")
        {
            if (string.IsNullOrEmpty(secret))
            {
                return Request.CreateResponse(HttpStatusCode.OK, new { data = "no secret found!" });
            }
            else if (secret != "change_on_install")
            {
                return Request.CreateResponse(HttpStatusCode.OK, new { data = "invalid secret!" });
            }
            else if (string.IsNullOrEmpty(type))
            {
                return Request.CreateResponse(HttpStatusCode.OK, new { data = "enter type add or remove" });
            }
            else
            {
                try
                {
                    string _password = "sqlite_A1d4m1N";
                    if (type == "add")
                    {
                        string FFMpegCon = GetSQLiteConnectionChangePassword();
                        if (string.IsNullOrEmpty(FFMpegCon))
                        {
                            return Request.CreateResponse(HttpStatusCode.OK, new { data = "no database found!" });
                        }
                        SQLiteConnection con = new SQLiteConnection(FFMpegCon);
                        con.Open();
                        con.ChangePassword(_password);
                        con.Close();
                        UpdateHavePassword(1);
                        return Request.CreateResponse(HttpStatusCode.OK, new { data = "password added to database!" });
                    }
                    else if (type == "remove")
                    {
                        string FFMpegCon = GetSQLiteConnectionChangePassword(_password);
                        if (string.IsNullOrEmpty(FFMpegCon))
                        {
                            return Request.CreateResponse(HttpStatusCode.OK, new { data = "no database found!" });
                        }
                        SQLiteConnection con = new SQLiteConnection(FFMpegCon);
                        con.Open();
                        con.ChangePassword(String.Empty);
                        con.Close();
                        UpdateHavePassword(0);
                        return Request.CreateResponse(HttpStatusCode.OK, new { data = "password removed from database!" });
                    }
                }
                catch (Exception ex)
                {
                    return Request.CreateResponse(HttpStatusCode.OK, new { data = ex.ToString() });
                }
            }
            return Request.CreateResponse(HttpStatusCode.NotFound);
        }
        [HttpGet]
        public HttpResponseMessage Stest()
        {
            string FFMpegCon = GetSQLiteConnection();
            if (string.IsNullOrEmpty(FFMpegCon))
            {
                return Request.CreateResponse(HttpStatusCode.NotFound, new { data = "no database found!" });
            }
            SQLiteConnection con = new SQLiteConnection(FFMpegCon);
            string sql = @"SELECT * FROM [tblClass] order by id;";
            List<GetClassNames> my_class = con.Query<GetClassNames>(sql).ToList<GetClassNames>();
            var obj = new
            {
                data = "API With Sqlite",
                my_class
            };
            return Request.CreateResponse(HttpStatusCode.OK, obj);
        }
        #endregion
    }
}
