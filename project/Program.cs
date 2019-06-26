using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using Mastonet;//Copyright(c) 2019 glacasa
using Mastonet.Entities;

namespace CliMa
{
    class Program
    {
        static readonly string path = Path.GetFullPath("i.secret"),
                               exec_path = Environment.CurrentDirectory + @"\cliMa.exe";

        /* 主機能の実装 */
        static void Main(string[] args)
        {
            Program cliMa = new Program();
            Console.SetWindowPosition(0, 0);
            Console.SetWindowSize(50, 30);
            Console.Clear();
            Console.WriteLine(@"
              __  __  __     __
    ____     / / /_/ /  \   /  \    ____
   / __ \   / / __  / /\ \ / /\ \  /  _ \/\
  / /  \_\ / / / / / /  \ V /  \ \ \ \ \   \
 / /  __  / / / / / /    \ /    \ \ \ \ \   \
/ /__/ / / / / / / /     | |     \ \ \ \_\   \
\_____/ /_/ /_/ /_/      |_|      \_\ \____/\_\");
            if (!File.Exists(path)) cliMa.RegistClient();
            MastodonClient client = cliMa.LoginClient();
            Console.WriteLine("\nLogin Succeeded.");
            TimelineStreaming stream = cliMa.StreamTimeline(client, TimelineType.Local);
            stream.Start();
            while (true)
            {
                string com = Console.ReadLine();
                if (com != String.Empty)
                {
                    string[] Input = com.Split(' ');
                    switch (Input[0])
                    {
                        case "timeline":
                            if (Input.Length > 1)
                            {
                                stream.Stop();
                                switch (Input[1])
                                {
                                    case "-l":
                                        stream = cliMa.StreamTimeline(client, TimelineType.Local);
                                        break;

                                    case "-h":
                                        stream = cliMa.StreamTimeline(client, TimelineType.Home);
                                        break;

                                    case "-f":
                                        stream = cliMa.StreamTimeline(client, TimelineType.Federation);
                                        break;

                                    default:
                                        ErrorMsg();
                                        break;
                                }
                                stream.Start();
                            }
                            else ErrorMsg();
                            break;

                        case "toot":
                            if (Input.Length > 1)
                            {
                                switch (Input.Length)
                                {
                                    case 2:
                                        client.PostStatus(Input[1], Visibility.Public);
                                        break;

                                    case 3:
                                        switch (Input[1])
                                        {
                                            case "-u":
                                                client.PostStatus(Input[2], Visibility.Unlisted);
                                                break;

                                            case "-p":
                                                client.PostStatus(Input[2], Visibility.Private);
                                                break;

                                            case "-d":
                                                client.PostStatus(Input[2], Visibility.Direct);
                                                break;

                                            default:
                                                ErrorMsg();
                                                break;
                                        }
                                        break;

                                    case 4:
                                        client.PostStatus(Input[3], Visibility.Public, sensitive: true, spoilerText: Input[2]);
                                        break;

                                    case 5:
                                        switch (Input[1])
                                        {
                                            case "-u":
                                                client.PostStatus(Input[4], Visibility.Unlisted, sensitive:true, spoilerText:Input[3]);
                                                break;

                                            case "-p":
                                                client.PostStatus(Input[4], Visibility.Private, sensitive: true, spoilerText: Input[3]);
                                                break;

                                            case "-d":
                                                client.PostStatus(Input[4], Visibility.Direct, sensitive: true, spoilerText: Input[3]);
                                                break;

                                            default:
                                                ErrorMsg();
                                                break;
                                        }
                                        break;
                                }
                            }
                            else ErrorMsg();
                            break;

                        case "help":
                            if (Input.Length > 1)
                            {
                                switch (Input[1])
                                {
                                    case "timeline":
                                        Console.WriteLine(@"
<Command> timeline:
'timeline [-l|-h|-f]'
 This Command changes Stream type.
 At Startup, Stream type is 'Local'
[Argument]
 -l: Set Stream type to Local Timeline
 -h: Set Stream type to Home Timeline
 -f: Set Stream type to Federation Timeline");
                                        break;

                                    case "toot":
                                        Console.WriteLine(@"
<Command> toot:
'toot [-u|-p|-d] [-s (SpoilerText)] (Content)'
This Command sends Toot Content.
Cannot Toot if Content is null.
Default Toot type is 'Public'
[Argument]
 -u: Set Toot type once to Unlisted
 -p: Set Toot type once to Private
 -d: Set Toot type once to Direct
 (SpoilerText) -s: Write SpoilerText to Toot");
                                        break;
                                }
                            }
                            else
                            {
                                Console.WriteLine(@"
<Command List>
-> timeline [-l|-h|-f]
-> toot [-u|-p|-d] [(SpoilerText) -s] (Content)
-> help
If you want more help, type 'help [timeline|toot]'");
                            }
                            break;
                        default:
                            ErrorMsg();
                            break;
                    }
                }
            }
        }

        //エラーメッセージの出力関数(コードの簡易化の為)
        private static void ErrorMsg()
        {
            Console.WriteLine(@"
This Command isn't exist in cliMa.
If you're using cliMa for the first time now,
Enter 'help' and Read about the command.");
        }


        /* アプリケーションの認証処理をする関数群の実装 */
        //Client(ここではcliMa)のMastodon側への登録とTokenの取得
        private void RegistClient()
        {
            string instance, mail, passwd;
            Console.WriteLine("Application registration (first time only).");
            Console.WriteLine("Enter the instance URL.");
            instance = Console.ReadLine().ToString();
            Console.WriteLine("Enter the Email address registered with the instance.");
            mail = Console.ReadLine().ToString();
            Console.WriteLine("Enter the Password registered for the instance.");
            passwd = ReadPassword();

            Console.WriteLine(path);
            var authClient = new AuthenticationClient(instance);
            var app = authClient.CreateApp("cliMa", Scope.Read | Scope.Write | Scope.Follow).Result;
            var auth = authClient.ConnectWithPassword(mail, passwd).Result;
            Console.WriteLine("Enter the Application Password for starting cliMa.");
            using (var cryptor = new Cryptor(ReadPassword()))//password input
            using (StreamWriter w = new StreamWriter(path))
            {
                w.WriteLine(Convert.ToBase64String(cryptor.Encode(app.Instance)));
                w.WriteLine(Convert.ToBase64String(cryptor.Encode(auth.AccessToken)));
            }
            Console.WriteLine("Registration completed.");
            Console.WriteLine("restarting this software.");
            Process.Start(exec_path);
            Environment.Exit(0);
        }

        //Mastodon側へのログイン処理とストリーム接続ソケットの引き渡し
        private MastodonClient LoginClient()
        {
            var App = new AppRegistration();
            var Auth = new Auth();
            Console.WriteLine("Enter the Application Password for starting cliMa.");
            using (StreamReader r = new StreamReader(path))
            {
                App.Instance = r.ReadLine();
                Auth.AccessToken = r.ReadLine();
            }
            using (var cryptor = new Cryptor(ReadPassword()))//password input
            {
                App.Instance = cryptor.Decode(Convert.FromBase64String(App.Instance));
                Auth.AccessToken = cryptor.Decode(Convert.FromBase64String(Auth.AccessToken));
            }
            var client = new MastodonClient(App, Auth);
            if (client.AuthToken != null)
            {
                //pass
            }
            return client;
        }


        /* Timelineストリームを出力する関数群 */
        //LocalTimelineのストリームの実装(コンソールの為画像は表示しない)
        enum TimelineType { Local, Home, Federation };

        private TimelineStreaming StreamTimeline(MastodonClient client, TimelineType type)
        {
            TimelineStreaming streaming = null;
            switch (type)
            {
                case TimelineType.Home:
                    streaming = client.GetUserStreaming();
                    break;

                default:
                    streaming = client.GetPublicStreaming();
                    break;
            }
            Console.WriteLine("\n\nStart fetching " + ((int)type == 0 ? "Local" : (int)type == 1 ? "Home" : "Federation") + " Timeline.");
            Console.WriteLine("================================================\n\n");
            streaming.OnUpdate += (sender, e) =>
            {
                StreamUpdateEventArgs s = type == TimelineType.Local ? !Regex.IsMatch(e.Status.Account.AccountName, ".+@.*") ? e : null : e;
                if (s != null)
                {
                    Console.WriteLine(s.Status.Account.DisplayName);
                    Console.WriteLine("@" + s.Status.Account.AccountName);
                    if (s.Status.SpoilerText != "")
                    {
                        Console.WriteLine("Note: " + s.Status.SpoilerText);
                    }
                    Console.WriteLine(HTML_Perser(s.Status.Content));
                    Console.WriteLine();
                }
            };
            return streaming;
        }


        /* その他の実装 */
        //HTMLパーサ(第一引数にHTMLコード、第二引数に特定タグ内の欲しい文字列[第二タグは無くてもOK])
        private string HTML_Perser(string HTML, string TAG = "")
        {
            HTML = Regex.Replace(HTML, "<br>", "\n");
            string REG = "<[^<>]+>|</.+>";
            return string.IsNullOrEmpty(TAG) ? Regex.Replace(HTML, REG, string.Empty) : Regex.Replace(Regex.Match(HTML, "<" + TAG + ">(.*)</" + TAG + ">").ToString(), REG, string.Empty);
        }

        //パスワード入力を受け取る関数
        public static string ReadPassword()
        {
            var password = new StringBuilder();

            while (true)
            {
                var keyinfo = Console.ReadKey(true);

                switch (keyinfo.Key)
                {
                    case ConsoleKey.Enter:
                        Console.WriteLine();
                        return password.ToString();

                    case ConsoleKey.Backspace:
                        if (0 < password.Length)
                            password.Length -= 1;
                        else
                            Console.Beep();
                        break;

                    default:
                        if (Char.IsLetter(keyinfo.KeyChar))
                        {
                            if ((keyinfo.Modifiers & ConsoleModifiers.Shift) == 0)
                            {
                                password.Append(keyinfo.KeyChar);
                            }
                            else
                            {
                                if (Console.CapsLock) password.Append(Char.ToLower(keyinfo.KeyChar));
                                else password.Append(Char.ToUpper(keyinfo.KeyChar));
                            }
                        }
                        else if (!Char.IsControl(keyinfo.KeyChar))
                        {
                            password.Append(keyinfo.KeyChar);
                        }
                        else
                        {
                            Console.Beep();
                        }
                        break;
                }
            }
        }
    }

    class Cryptor : IDisposable
    {
        public Cryptor(string password)
        {
            using (var hash = new SHA512Managed())
            {
                var data = hash.ComputeHash(Encoding.UTF8.GetBytes(password));
                Algorithm.KeySize = 256;
                Algorithm.Key = data.Take(32).ToArray();
                Algorithm.IV = data.Skip(32).Take(16).ToArray();
            }
        }

        protected SymmetricAlgorithm Algorithm { get; private set; } = new AesManaged();

        public byte[] Encode(string text)
        {
            using (var memoryStream = new MemoryStream())
            {
                using (var cryptoStream = new CryptoStream(memoryStream, Algorithm.CreateEncryptor(), CryptoStreamMode.Write))
                {
                    var buffer = Encoding.UTF8.GetBytes(text);
                    cryptoStream.Write(buffer, 0, buffer.Length);
                }
                return memoryStream.ToArray();
            }
        }

        public string Decode(byte[] encoded)
        {
            using (var memoryStream = new MemoryStream(encoded))
            using (var cryptoStream = new CryptoStream(memoryStream, Algorithm.CreateDecryptor(), CryptoStreamMode.Read))
            using (var reader = new StreamReader(cryptoStream, Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }

        public void Dispose()
        {
            Algorithm?.Dispose();
            Algorithm = null;
        }
    }
}