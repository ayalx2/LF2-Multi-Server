using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;
using System.Linq;

namespace LF2MultiServerConsole
{
    class LF2MultiServer
    {
        private static int MINIMUM_PLAYERS_COUNT = 1;
        private static int MINIMUM_PLAYERS_COUNT_IN_GAME = 2;
        private static int MAXIMUM_PLAYERS_COUNT = 8;

        private static String START_CONNECTION_BUFFER = "u can connect\0";
        private static int PLAYERS_LIST_BUFFER_SIZE = 77;
        private static int RANDOM_DATA_BUFFER_SIZE = 3001;
        private static int RANDOM_DATA_PLAYERS_NAMES_START = 300;
        private static int RANDOM_DATA_PLAYERS_NAMES_GAP = 40;
        private static int NORMAL_BUFFER_SIZE = 22;
        private static byte LF2_SHIFT_OPCODE = 5;

        private static int PING_COMMAND_LOOPS_PER_PLAYER = 30;
        private static int PING_LF2_WAIT_BETWEEN_FRAMES = 33;

        private static int COMMANDS_INPUT_THREAD_INIT_WAIT = 1000;

        private static List<LF2Player> players;
        private static int activePlayersCount;
        private static bool serverActive = false;

        private static bool pingCommandActive = false;
        private static int pingPlayerIndex = 0;
        private static int pingPlayerLoopsCount = 0;
        private static int pingTotalLoopsCount = 0;
        private static ManualResetEvent pingEventFinsihed = new ManualResetEvent(false);

        private static DateTime gameBeginTime;
        private static Thread commandsInputThread = new Thread(new ThreadStart(StartCommandsInput));

        private static Random random = new Random();

        private static List<TcpListener> listeners;

        static void ShuffleList(List<LF2Player> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = random.Next(n + 1);
                LF2Player value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        static bool IsWinVistaOrHigher()
        {
            OperatingSystem OS = Environment.OSVersion;
            return (OS.Platform == PlatformID.Win32NT) && (OS.Version.Major >= 6);
        }

        private static List<IPAddress> GetAllHostIps()
        {
            List<IPAddress> hosts = new List<IPAddress>();

            IPHostEntry host;
            host = Dns.GetHostEntry(Dns.GetHostName());

            foreach (IPAddress ip in host.AddressList)
            {
                //InterNetwork - IPv4
                //AddressFamily.InterNetworkV6 - IPv6
                //As lf2.exe does not support IPv6, there is no need to take Ipv6 addresses too.
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    hosts.Add(ip);
                }
            }

            //in Windows Vista and above, 127.0.0.1 is taken as a seperate interface.
            //IPAddress.Any will lock on it.
            if (IsWinVistaOrHigher())
            {
                hosts.Add(IPAddress.Any);
            }

            return hosts;
        }

        //bind all ip addresses.
        //why don't just bind IPAddress.Any (0.0.0.0)?
        //beacuse it is not binding all addresses, but only "the most suitable address"
        //I know that some documentatio on the internet says it binds all available addresses, but apparently it is not true.
        //(for example, MSDN for C++ and C# do not agree)
        private static void StartListenAllInterfaces()
        {
            List<IPAddress> hosts = GetAllHostIps();
            listeners = new List<TcpListener>(hosts.Count);

            foreach (IPAddress host in hosts)
            {
                TcpListener tcpListener = new TcpListener(host, 12345);

                try
                {
                    tcpListener.Start();
                    listeners.Add(tcpListener);
                }
                catch(SocketException e)
                {
                    MyConsole.WriteLine(ConsoleColor.Yellow, "Warning: can not bind ip address " + host.ToString());

                    string exceptionMessage;
                    if (e.ErrorCode == SocketsUtils.SOCKET_ERROR_PORT_ALREADY_OPENED)
                    {
                        exceptionMessage = "The port 12345 is already in use. If LF2 is currently running, close it and then try again.";
                    }
                    else
                    {
                        exceptionMessage = e.Message;
                    }
                    MyConsole.WriteLine(ConsoleColor.Yellow, exceptionMessage);
                    MyConsole.NewLine();
                }
            }

            //probably it won't happen, beacause lf2 binds only one network interface...
            if (hosts.Count == 0)
            {
                throw new SocketException(SocketsUtils.SOCKET_ERROR_PORT_ALREADY_OPENED);
            }
        }

        private static void StopListenAllInterfaces()
        {
            if (listeners != null)
            {
                for (int i = 0; i < listeners.Count; ++i)
                {
                    try
                    {
                        listeners[i].Stop();
                    }
                    catch (Exception) { }
                }
            }
        }

        private static Socket AcceptPlayerSocket()
        {
            while (true)
            {
                foreach (TcpListener listener in listeners)
                {
                    if (listener.Pending())
                    {
                        Socket client = listener.AcceptSocket();

                        //Disable the Nagle Algorithm for this tcp socket.
                        //it shouldn't have effect on lf2 protocol, because lf2 always receives before it sends another message.
                        //but just to be on the safe side.
                        client.NoDelay = true;

                        return client;
                    }
                }

                Thread.Sleep(1000);
            }
        }

        private static void SendAllPlayers(byte[] data)
        {
            for (int i = 0; i < players.Count; ++i)
            {
                SocketsUtils.SendAll(players[i].Client, data);
            }
        }

        private static void SendAllPlayers(String data)
        {
            for (int i = 0; i < players.Count; ++i)
            {
                SocketsUtils.SendAll(players[i].Client, data);
            }
        }

        //receives input from player, and measures the time.
        //used by Ping command.
        private static void ReceviceFromPlayer(byte[] dataBuffer, LF2Player player, out TimeSpan time)
        {
            DateTime startReceiveTime = DateTime.Now;
            ReceviceFromPlayer(dataBuffer, player);
            time = DateTime.Now - startReceiveTime;
        }


        //receives input from player.
        //responsible to handle a diconnection of the player.
        private static void ReceviceFromPlayer(byte[] dataBuffer, LF2Player player)
        {
            if (player.IsActive)
            {
                try
                {
                    SocketsUtils.ReceiveAll(player.Client, dataBuffer, NORMAL_BUFFER_SIZE);
                }
                catch (Exception e)
                {
                    MyConsole.WriteLine(ConsoleColor.Yellow, "Error: {0}", e.Message);
                    MyConsole.WriteLine("Player number {0}, \"{1}\", has disconnected.\n", player.PlayerIndex + 1, player.PlayerName);
                    player.IsActive = false;

                    player.LastFInput1 = 1;
                    player.LastFInput2 = 1;
                    player.LastKeyInput = 1;
                }

                //get key input
                player.LastKeyInput = dataBuffer[player.PlayerIndex];
                
                //get F keys
                //Fkey 1 - index 10
                //Fkey 2 - index 12
                player.LastFInput1 = dataBuffer[10];
                player.LastFInput2 = dataBuffer[12];
            }
            else
            {
                if (player.OutCount != 0)
                {
                    if (player.OutCount == 6)
                    {
                        //decrease the number of current online players
                        --activePlayersCount;
                        try
                        {
                            player.Client.Close();
                        }
                        catch (Exception) { }
                        if (activePlayersCount < MINIMUM_PLAYERS_COUNT_IN_GAME)
                        {
                            throw new Exception("All players has been disconnected.");
                        }
                    }

                    player.LastFInput1 = 1;
                    player.LastFInput2 = 1;
                    player.LastKeyInput = 1;

                    //send "jump" from this user to the other clients.
                    //if the user was choosing a player when he has suddenly disconnected,
                    //it will cancel his choosing so the game will not stuck for the other players.
                    if (player.OutCount % 2 == 0)
                    {
                        player.LastKeyInput = LF2_SHIFT_OPCODE;
                    }

                    --player.OutCount;
                }
            }
        }

        //sends output to player.
        //responsible to handle a diconnection of the player.
        private static void SendToPlayer(byte[] dataBuffer, LF2Player player)
        {
            if (player.IsActive)
            {
                try
                {
                    SocketsUtils.SendAll(player.Client, dataBuffer);
                }
                catch (Exception e)
                {
                    MyConsole.WriteLine(ConsoleColor.Yellow, "Error: {0}", e.Message);
                    MyConsole.WriteLine("Player number {0}, \"{1}\", has disconnected.\n", player.PlayerIndex + 1, player.PlayerName);
                    player.IsActive = false;
                }
            }
        }

        static String GetPlayerNicknameByIndex(String names, int playerIndex)
        {
            return names.Substring(32 + (11 * playerIndex), 11);
        }

        private static void StartCommandsInput()
        {
            Thread.Sleep(COMMANDS_INPUT_THREAD_INIT_WAIT);

            MyConsole.NewLine();
            MyConsole.WriteLine(ConsoleColor.Cyan, "Type \"players\" to copy players list to the clipboard.");
            MyConsole.WriteLine(ConsoleColor.Cyan, "Type \"remove x\" to remove a player from the game (x - the player index).");
            MyConsole.WriteLine(ConsoleColor.Cyan, "Type \"ping\" to detecet the player who has the slowest connection to the server.");
            MyConsole.WriteLine(ConsoleColor.Cyan, "Type \"random\" to get the players list in a random order.");

            while (true)
            {
                MyConsole.NewLine();

                String userInput = Console.ReadLine();
                userInput = userInput.ToLower();

                if (!serverActive)
                {
                    break;
                }

                if (userInput == "players")
                {
                    String playersList = "";
                    for (int i = 0; i < players.Count; ++i)
                    {
                        string isDisconnected = (!players[i].IsActive) ? " (disconnected)" : "";
                        playersList += String.Format("Player number {0:D}: \"{1}\"" + isDisconnected + Environment.NewLine, i + 1, players[i].PlayerName);
                    }
                    Clipboard.Clear();
                    Clipboard.SetText(playersList);

                    MyConsole.WriteLine(playersList);
                    MyConsole.WriteLine("The list has been copied into your clipboard.\nPress \"ctrl+v\" in order to paste.");
                }
                else if (userInput == "ping")
                {
                    MyConsole.WriteLine("Analyzing connections, plaese wait...");

                    for (int i = 0; i < players.Count; ++i)
                    {
                        players[i].TotalWaitAmount = 0;
                    }

                    pingPlayerIndex = 0;
                    pingPlayerLoopsCount = 0;
                    pingTotalLoopsCount = 0;
                    pingEventFinsihed.Reset();
                    pingCommandActive = true;

                    //wait for data gathrering to complete
                    pingEventFinsihed.WaitOne();

                    //check that the game is still running after the sleep
                    if (!serverActive)
                    {
                        return;
                    }

                    for (int i = 0; i < players.Count; ++i)
                    {
                        //Make average
                        players[i].TotalWaitAmount = players[i].TotalWaitAmount / PING_COMMAND_LOOPS_PER_PLAYER;

                        //LF2 has a inner clock, and it delays its respond if the request was sent quicker that its FPS ratio.
                        //So I subtract this ratio from the total ping result.
                        //Apparently LF2 waits 2 frames before it responds.
                        //I don't sure why. I will check it again when I have some time.
                        players[i].TotalWaitAmount -= PING_LF2_WAIT_BETWEEN_FRAMES * 2;
                        if (players[i].TotalWaitAmount < 0) players[i].TotalWaitAmount = 0;
                    }

                    List<LF2Player> sortedList = players.OrderByDescending(o => o.TotalWaitAmount).ToList();

                    MyConsole.NewLine();
                    MyConsole.WriteLine("Delay times for players, starting from the slowest player:");
                    MyConsole.WriteLine(ConsoleColor.Cyan, "(Zero indicates that the player does not create lags)");
                    MyConsole.NewLine();

                    for (int i = 0; i < sortedList.Count; ++i)
                    {
                        if (sortedList[i].IsActive)
                            MyConsole.WriteLine(sortedList[i].PlayerName + "\t\t - " + Convert.ToInt32(sortedList[i].TotalWaitAmount) + "ms");
                    }
                }
                else if (userInput.StartsWith("remove ") && userInput.Length == 8)
                {
                    try
                    {
                        int playerIndex = int.Parse(userInput[7].ToString());
                        if (playerIndex < 1 || playerIndex > players.Count)
                        {
                            throw new Exception();
                        }

                        LF2Player playerToRemove = players[playerIndex - 1];

                        if (!playerToRemove.IsActive)
                        {
                            MyConsole.WriteLine(playerToRemove.PlayerName + " has already disconnected.");
                            continue;
                        }

                        playerToRemove.IsActive = false;
                        MyConsole.WriteLine(playerToRemove.PlayerName + " has been removed from the game.");
                    }
                    catch (Exception)
                    {
                        MyConsole.WriteLine("Invalid player index.");
                    }
                }
                else if (userInput == "random")
                {
                    List<LF2Player> randomList = players.ToList();
                    ShuffleList(randomList);

                    String playersShuffledList = "";
                    for (int i = 0; i < players.Count; ++i)
                    {
                        if (randomList[i].IsActive)
                            playersShuffledList += randomList[i].PlayerName + Environment.NewLine;
                    }

                    MyConsole.WriteLine("\nPlayers by random order:");
                    MyConsole.WriteLine(playersShuffledList);

                    Clipboard.Clear();
                    Clipboard.SetText(playersShuffledList);

                    MyConsole.WriteLine("The list has been copied into your clipboard.\nPress \"ctrl+v\" in order to paste.");
                }
                else if (userInput == "hipy")
                {
                    MyConsole.WriteLine("hipy tov!");
                }
                else
                {
                    MyConsole.WriteLine("Invalid command.");
                }
            }
        }

        private static void HandleCycle(byte[] inputBuffer, ref byte playerFInput1, ref byte playerFInput2)
        {
            //receive input from all players
            for (int i = 0; i < players.Count; ++i)
            {
                ReceviceFromPlayer(inputBuffer, players[i]);

                //get F keys
                //Still - if 2 users in the very same moment press 2 diffrent F Keys, the game will be disconnected.
                if (players[i].LastFInput1 != 1)
                {
                    playerFInput1 = players[i].LastFInput1;
                }
                if (players[i].LastFInput2 != 1)
                {
                    playerFInput2 = players[i].LastFInput2;
                }
            }

            //prepare output with the players move
            for (int i = 0; i < players.Count; ++i)
            {
                inputBuffer[i] = players[i].LastKeyInput;
            }
            //add the f keys
            inputBuffer[10] = playerFInput1;
            inputBuffer[12] = playerFInput2;

            //send output to all players
            for (int i = 0; i < players.Count; ++i)
            {
                SendToPlayer(inputBuffer, players[i]);
            }
        }

        private static void HandleCyclePingEnabled(byte[] inputBuffer, ref byte playerFInput1, ref byte playerFInput2)
        {
            //ping command is active
            //the idea is: Send() does not take a lot of time, because it just push the data to a buffer and the OS sends it later.
            //so I check only the time Receive() takes.
            //because I want to get the time that took the socket to respond, I have to receive its data first, before the others, and send his data last.

            //if we have already sampled the player 10 times, move to the next player.
            if (pingPlayerLoopsCount > PING_COMMAND_LOOPS_PER_PLAYER)
            {
                pingPlayerIndex++;
                pingPlayerLoopsCount = 0;
            }

            //if we have already went over on all the players, we finished.
            if (pingPlayerIndex >= players.Count)
            {
                pingCommandActive = false;
                pingEventFinsihed.Set();
                return;
            }

            //if player is not active, continue to the next one
            if (!players[pingPlayerIndex].IsActive)
            {
                pingPlayerIndex++;
                pingPlayerLoopsCount = 0;
                return;
            }

            pingTotalLoopsCount++;
            pingPlayerLoopsCount++;

            //recevie data from the current player
            LF2Player player = players[pingPlayerIndex];
            TimeSpan receiveTime;
            ReceviceFromPlayer(inputBuffer, player, out receiveTime);

            //sum the time. on the first loop, don't sum it because the send order was incorrect.
            if (pingPlayerLoopsCount != 1)
            {
                player.TotalWaitAmount += receiveTime.TotalMilliseconds;
            }

            if (player.LastFInput1 != 1)
            {
                playerFInput1 = player.LastFInput1;
            }
            if (player.LastFInput2 != 1)
            {
                playerFInput2 = player.LastFInput2;
            }

            //recevie data from the others players
            for (int i = 0; i < players.Count; ++i)
            {
                if (i == pingPlayerIndex) continue;

                ReceviceFromPlayer(inputBuffer, players[i], out receiveTime);

                if (players[i].LastFInput1 != 1)
                {
                    playerFInput1 = players[i].LastFInput1;
                }
                if (players[i].LastFInput2 != 1)
                {
                    playerFInput2 = players[i].LastFInput2;
                }
            }

            //prepare output with the players move
            for (int i = 0; i < players.Count; ++i)
            {
                inputBuffer[i] = players[i].LastKeyInput;
            }
            //add the f keys
            inputBuffer[10] = playerFInput1;
            inputBuffer[12] = playerFInput2;

            //send output to all players - the current pinged player is sent last
            for (int i = 0; i < players.Count; ++i)
            {
                if (i == pingPlayerIndex) continue;
                SendToPlayer(inputBuffer, players[i]);
            }

            SendToPlayer(inputBuffer, player);
        }

        private static void HandleGameCycles()
        {
            byte[] inputBuffer = new byte[NORMAL_BUFFER_SIZE];
            byte playerFInput1;
            byte playerFInput2;

            while (true)
            {
                //init f keys
                playerFInput1 = 1;
                playerFInput2 = 1;

                if (!pingCommandActive)
                {
                    HandleCycle(inputBuffer, ref playerFInput1, ref playerFInput2);
                }
                else
                {
                    HandleCyclePingEnabled(inputBuffer, ref playerFInput1, ref playerFInput2);
                }
            }
        }

        private static void StartServer()
        {
            MyConsole.WriteLine(ConsoleColor.Cyan, "Type players count (between 1 to 8), and then press enter to continue.");
            MyConsole.Write(ConsoleColor.Cyan, "Players count: ");
            int clientsCount = 0;
            try
            {
                clientsCount = int.Parse(Console.ReadLine());
                if (clientsCount < MINIMUM_PLAYERS_COUNT || clientsCount > MAXIMUM_PLAYERS_COUNT)
                {
                    throw new Exception();
                }
            }
            catch (Exception)
            {
                throw new Exception("Incorrect players count.");
            }

            StartListenAllInterfaces();

            MyConsole.WriteLine("Server has been activated.");

            ConsoleColor[] playersColors = new ConsoleColor[8];
            playersColors[0] = ConsoleColor.Green;
            playersColors[1] = ConsoleColor.Red;
            playersColors[2] = ConsoleColor.Magenta;
            playersColors[3] = ConsoleColor.Blue;
            playersColors[4] = ConsoleColor.DarkGreen;
            playersColors[5] = ConsoleColor.DarkRed;
            playersColors[6] = ConsoleColor.DarkMagenta;
            playersColors[7] = ConsoleColor.DarkBlue;

            players = new List<LF2Player>(clientsCount);
            activePlayersCount = clientsCount;
            while (players.Count < clientsCount)
            {
                MyConsole.NewLine();
                MyConsole.WriteLine("Waiting for player number {0:D} to connect...", players.Count + 1);

                Socket client = AcceptPlayerSocket();

                MyConsole.WriteLine(playersColors[players.Count], "Player number {0:D} has connected from {1}.", players.Count + 1, client.RemoteEndPoint.ToString().Split(':')[0]);

                players.Add(new LF2Player(client));
            }

            MyConsole.NewLine();
            MyConsole.WriteLine("All players has connected.");

            //all players has connected, we can stop listening now.
            StopListenAllInterfaces();

            SendAllPlayers(START_CONNECTION_BUFFER);

            //get players names
            String nicknamesList = "";
            for (int i = 0; i < players.Count; ++i)
            {
                String playersList;
                String playerName;
                SocketsUtils.ReceiveAll(players[i].Client, out playersList, PLAYERS_LIST_BUFFER_SIZE);

                //all the players use controls of player 1
                playerName = GetPlayerNicknameByIndex(playersList, 0);

                players[i].SetPlayerName(playerName);
                players[i].PlayerIndex = i;

                //4 names is sent by the normal connection string.
                //the 4 others (if exists) is sent in the random buffer. 
                if (i < 4)
                {
                    nicknamesList += playerName;
                }
            }

            //preapre nicknames list to be sent to the players, add empty slots for names that are not used.
            //this list contains the first 4 players names.
            for (int i = players.Count; i < 4; ++i)
            {
                nicknamesList += "___________";
            }
            nicknamesList += "\0";

            MyConsole.NewLine();
            MyConsole.WriteLine("Server has made connections with {0:D} players.", players.Count);

            for (int i = 0; i < players.Count; ++i)
            {
                MyConsole.Write(playersColors[i], "Player number {0:D}: ", i + 1);
                MyConsole.WriteLine(players[i].PlayerName);
            }

            //send for each player which input he is going to use.
            for (int i = 0; i < players.Count; ++i)
            {
                String clientPlayersInput = TextUtils.SetChar("11111111000000000000000000000000", i, '0') + nicknamesList;
                SocketsUtils.SendAll(players[i].Client, clientPlayersInput);
            }

            //create a random buffer (this buffer will be used to randomize actions in the game)
            byte[] randomData = new byte[RANDOM_DATA_BUFFER_SIZE];
            random.NextBytes(randomData);

            //add the 4 other players names - if exists.
            //lf2 supports sending only 4 player names, so in order to send another 4 names I have to do it somewhere else.
            //so I encode the 4 other names in the Random buffer!
            //
            //to encode the names of the last 4 players, I do the follows:
            //  - jump to byte 300
            //  - write the first name in jumps of one byte ('a',random byte,'b',random byte'...) until 10 bytes are written, or null to indicate end of name.
            //  - jump 40 bytes, and write the next name. and so on.
            //
            //in this way, the buffer is still very haphazard, so it wouldn't be noticed at all.
            for (int i = 4; i < players.Count; ++i )
            {
                int start = RANDOM_DATA_PLAYERS_NAMES_START + (i - 4) * RANDOM_DATA_PLAYERS_NAMES_GAP;
                for (int t = 0, s = 0; t < players[i].PlayerName.Length + 1; ++t, s+=2)
                {
                    if (t != players[i].PlayerName.Length)
                    {
                        randomData[start + s] = (byte)players[i].PlayerName[t];
                    }
                    else
                    {
                        randomData[start + s] = 0; //end of name
                    }
                }
            }

            //send the random buffer
            SendAllPlayers(randomData);

            MyConsole.Write("\nThe game has started!");
            gameBeginTime = DateTime.Now;

            serverActive = true;

            //start a new thread for get commands from the user
            commandsInputThread.IsBackground = false;
            commandsInputThread.SetApartmentState(ApartmentState.STA);
            commandsInputThread.Start();

            //start the game!
            HandleGameCycles();
        }

        //start the server!
        public static void Start()
        {
            String exceptionMessage = "";
            int exceptionErrorCode = 0;

            try
            {
                StartServer();
            }
            catch (SocketException e)
            {
                exceptionMessage = e.Message;
                exceptionErrorCode = e.ErrorCode;
            }
            catch (Exception e)
            {
                exceptionMessage = e.Message;
            }

            //close all the conenctions to the players
            if (players != null)
            {
                for (int i = 0; i < players.Count; ++i)
                {
                    try
                    {
                        players[i].Client.Close();
                    }
                    catch (Exception) { }
                }
            }

            StopListenAllInterfaces();

            //check for soame special exceptions
            if (exceptionErrorCode == SocketsUtils.SOCKET_ERROR_PORT_ALREADY_OPENED)
            {
                exceptionMessage = "The port 12345 is already in use. If LF2 is currently running, close it and then try again.";
            }

            //report the error to the user.
            MyConsole.WriteLine(ConsoleColor.Yellow, "Error: {0}", exceptionMessage);

            if (serverActive)
            {
                MyConsole.Write("Game is over. ");

                TimeSpan duration = DateTime.Now - gameBeginTime;
                MyConsole.WriteLine(ConsoleColor.Magenta, "Game duration: " + duration.ToString(@"hh\:mm") );

                MyConsole.WriteLine("\nPress any key to continue...");
                
                serverActive = false;
                pingEventFinsihed.Set(); //in case ping command is running
            }
            else
            {
                MyConsole.WriteLine("\nPress any key to continue...");
                if (!serverActive)
                {
                    Console.ReadKey(true);
                }
            }
        }
    }
}
