using System;
using System.Windows;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using static BLIO;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace BorderlandsCommander
{



    public class UdpControl
    {
        public static IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);
        public static IntPtr BlWindowHandle = INVALID_HANDLE_VALUE;

        private int listenPort = 13666;

        private UdpClient udpClient;
        private IPEndPoint RemoteIpEndPoint;


        public UdpControl()
        {
            listenPort = Properties.Settings.Default.udpPort;
#if DEBUG
            Console.WriteLine("Udp Listen on {0}", listenPort);
#endif

            // IPv4 and IPv6 dual-mode socket    
            var socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);

            socket.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);
            socket.Bind(new IPEndPoint(IPAddress.Parse("::"), listenPort));

            udpClient = new UdpClient();
            udpClient.Client = socket;
            RemoteIpEndPoint = new IPEndPoint(IPAddress.Any, 0);

            udpClient.BeginReceive(AsyncCallbackReceive, null);

        }

        void AsyncCallbackReceive(IAsyncResult ar)
        {
            byte[] receiveBytes = udpClient.EndReceive(ar, ref RemoteIpEndPoint);
            try
            {
                string receiveString = Encoding.UTF8.GetString(receiveBytes);
#if DEBUG
                Console.WriteLine("Recv {0} bytes from {1} port {2}", receiveBytes.Length, RemoteIpEndPoint.Address.ToString(), RemoteIpEndPoint.Port);
#endif
                if (receiveBytes.Length > 1)
                {
#if DEBUG
                    Console.WriteLine("{0}", receiveString);
#endif
                    Parse(receiveString);
                }
            } catch (System.Exception e)
            {
#if DEBUG
                Console.WriteLine(e.ToString());
                
#else   
                
#endif
            }
            udpClient.BeginReceive(AsyncCallbackReceive, null);
        }


        private static Lazy<Regex> CommandPattern = new Lazy<Regex>(() => new Regex($@"^([^ ]+) (.+)$", RegexOptions.Compiled));
        private static Lazy<Regex> MapNamePattern = new Lazy<Regex>(() => new Regex($@"^([^ ]+)\.(.+)", RegexOptions.Compiled));

        private static Lazy<Regex> RestorePosPattern = new Lazy<Regex>(() => new Regex($@"Map\:(.+)\;Pos\:(.+)\;Rot:(.+)$", RegexOptions.Compiled));

        void Parse(string receiveString)
        {
            var parsematch = CommandPattern.Value.Match(receiveString);
            System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
            if (!parsematch.Success) return;

            var command = parsematch.Groups[1].Value;
#if DEBUG
            Console.WriteLine("Command: {0}", command);
#endif

            switch (command)
            {
                case "PING?":
                    Ping();
                    break;

                case "GETPOS?":
                    SendPostition();
                    break;

                case "SAVEPOS?":
                    SavePosition();
                    break;

                case "RESTOREPOS":
                    RestorePosition(parsematch.Groups[2].Value);
                    break;

                case "MAPNAME?":
                    SendMapName();
                    break;

                case "COMMAND":
                    CustomCommand(parsematch.Groups[2].Value);
                    break;

                case "DAMAGENUM":
                    DamageNumbers(parsematch.Groups[2].Value);
                    break;

                case "HALFSPEED":
                    App.HalveSpeed();
                    break;

                case "DOUBLESPEED":
                    App.DoubleSpeed();
                    break;

                case "NORMALSPEED":
                    App.ResetSpeed();
                    break;

                case "PLAYERSONLY":
                    PlayerOnly(parsematch.Groups[2].Value);
                    break;

                case "MOVEFORWARD":
                    App.MoveForwardBackward(double.Parse(parsematch.Groups[2].Value));
                    break;

                case "MOVELEFTRIGHT":
                    App.MoveLeftRight(double.Parse(parsematch.Groups[2].Value));
                    break;

                default:
#if DEBUG
                    Console.WriteLine("Unknown Command: {0}", command);
#endif
                    break;
            }

        }

        void SendMapName()
        {
            var controller = BLObject.GetPlayerController();
            // If we could not, stop and present an error.
            if (controller == null)
                return;
            var pawn = controller["Pawn"] as BLObject;
            if (pawn == null) return;
            pawn.UsePropertyMode = BLObject.PropertyMode.GetAll;
          
            var info = pawn["WorldInfo"] as BLObject;
            if (info == null) return;
            info.UsePropertyMode = BLObject.PropertyMode.GetAll;

            var mapLevel = info["CommittedPersistentLevel"] as BLObject;

            var mapMatch = MapNamePattern.Value.Match(mapLevel.Name);
            if (!mapMatch.Success) return;
            var mapName = mapMatch.Groups[1];

            SendUDP(String.Format("MapName Map:{0}", mapName));

        }


        void SendUDP(string sendStr)
        {
            byte[] packet = Encoding.UTF8.GetBytes(sendStr);
            udpClient.Send(packet, packet.Length, RemoteIpEndPoint);

        }

        void Ping()
        {
            string hostName = System.Net.Dns.GetHostName();

            SendUDP(String.Format("PING hostname:{0}", hostName));

        }

        void SendPostition( )
        {
            string location;
            string rotation;
            string mapName;
            App.GetSavedPositionStrings(out location, out rotation, out mapName);

            if (mapName == null) return;

            var mapMatch = MapNamePattern.Value.Match(mapName);

            if (!mapMatch.Success) return;

            mapName = mapMatch.Groups[1].Value;

            SendUDP(String.Format("SavedPosition Map:{0};Pos:{1};Rot:{2}", mapName, location, rotation));
        }

        void SavePosition()
        {
            App.SavePosition();

            SendPostition();
        }

        void RestorePosition(string posData)
        {
            var restoreMatch = RestorePosPattern.Value.Match(posData);
            if (!restoreMatch.Success) return;

            var controller = BLObject.GetPlayerController();
            if (controller == null) return;

            // Get the object for the pawn from the player controller.
            var pawn = controller["Pawn"] as BLObject;
            // If we could not, stop and present an error.
            if (pawn == null || pawn.Class != "WillowPlayerPawn")
                return;

            string mapname = restoreMatch.Groups[1].Value;
            string location = restoreMatch.Groups[2].Value;
            string rotation = restoreMatch.Groups[3].Value;

            //TODO compare Map Names
            string command = $"set {controller.Name} Rotation {rotation}|set {pawn.Name} Location {location}";
            App.PerformAction(command, "Set position");

            SendUDP("OK RESTOREPOS");
        }

        void CustomCommand(string command)
        {
            var result=RunCommand(command);
            //if (App.ShowFeedback) RunCommand("Say {0}", command);
            StringBuilder builder = new StringBuilder();
            builder.AppendLine(string.Format("CommandResults {0}", result.Count));
            for (int i = 0; i < result.Count; i++) builder.AppendLine(result[i]);
            SendUDP(builder.ToString());
        }

        void DamageNumbers(string display)
        {

            App.ShowDamageNumbers = display.StartsWith("0");
            App.ToggleDamageNumbers();
        }


        void PlayerOnly(string flags)
        {
            App.PlayersOnly = flags.StartsWith("0");
            App.TogglePlayersOnly();
        }
        


    }
}
