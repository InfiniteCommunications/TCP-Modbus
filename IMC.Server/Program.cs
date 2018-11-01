using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Configuration;
using System.Collections.Specialized;
using System.Data;
using System.Data.SQLite;
using EasyModbus;
using System.Text.RegularExpressions;

// State object for reading client data asynchronously  
public class StateObject
{
    // Client  socket.  
    public Socket workSocket = null;
    // Size of receive buffer.  
    public const int BufferSize = 1024;
    // Receive buffer.  
    public byte[] buffer = new byte[BufferSize];
    // Received data string.  
    public StringBuilder sb = new StringBuilder();

}
public class AsynchronousSocketListener
{
    // Thread signal.  
    public static ManualResetEvent allDone = new ManualResetEvent(false);

    private static SQLiteConnection sql_con;
    private static SQLiteCommand sql_cmd;
    private static SQLiteDataAdapter DB;

    private static void SetConnection()
    {
        sql_con = new SQLiteConnection
            ("Data Source=trialTempData.db;Version=3;New=False;Compress=True;");
    }
    private static void ExecuteQuery(string txtQuery)
    {
        SetConnection();
        sql_con.Open();
        sql_cmd = sql_con.CreateCommand();
        sql_cmd.CommandText = txtQuery;
        sql_cmd.ExecuteNonQuery();
        sql_con.Close();
    }
    public AsynchronousSocketListener()
    {
    }

    public static bool IsValidateIP(string Address)
    {
        //Match pattern for IP address    
        string Pattern = @"^([1-9]|[1-9][0-9]|1[0-9][0-9]|2[0-4][0-9]|25[0-5])(\.([0-9]|[1-9][0-9]|1[0-9][0-9]|2[0-4][0-9]|25[0-5])){3}$";
        //Regular Expression object    
        Regex check = new Regex(Pattern);

        //check to make sure an ip address was provided    
        if (string.IsNullOrEmpty(Address))
            //returns false if IP is not provided    
            return false;
        else
            //Matching the pattern    
            return check.IsMatch(Address, 0);
    }

    public static void StartListening()
    {
        // Establish the local endpoint for the socket.  
        // The DNS name of the computer  
        // running the listener is "host.contoso.com".  
        //IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
        //IPAddress ipAddress = ipHostInfo.AddressList[0];
        //IPEndPoint localEndPoint = new IPEndPoint(ipAddress, 11000);


        Console.WriteLine("Please enter your TCP Server IP to start the application service : ");
        string userinputIp = Console.ReadLine();
        if (!IsValidateIP(userinputIp))
        {
            Console.WriteLine("Please check and enter again your TCP Server IP to start the application service :");
            userinputIp = Console.ReadLine();
        }

        IPAddress ipAddress = IPAddress.Parse(userinputIp);
        IPEndPoint localEndPoint = new IPEndPoint(ipAddress,15800);

        // Create a TCP/IP socket.  
        Socket listener = new Socket(AddressFamily.InterNetwork,
            SocketType.Stream, ProtocolType.Tcp);

        // Bind the socket to the local endpoint and listen for incoming connections.  
        try
        {
            listener.Bind(localEndPoint);
            listener.Listen(100);

            while (true)
            {
                // Set the event to nonsignaled state.  
                allDone.Reset();

                // Start an asynchronous socket to listen for connections.  
                Console.WriteLine("TCP Server : "+userinputIp+" listening on Port :15800  Waiting for a connection...");
                listener.BeginAccept(
                    new AsyncCallback(AcceptCallback),
                    listener);

                // Wait until a connection is made before continuing.  
                allDone.WaitOne();
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
        }

        Console.WriteLine("\nPress ENTER to continue...");
        Console.Read();

    }
    public static void AcceptCallback(IAsyncResult ar)
    {
        // Signal the main thread to continue.  
        allDone.Set();

        // Get the socket that handles the client request.  
        Socket listener = (Socket)ar.AsyncState;
        Socket handler = listener.EndAccept(ar);

        // Create the state object.  
        StateObject state = new StateObject();
        state.workSocket = handler;
        handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
            new AsyncCallback(ReadCallback), state);
    }
    public static void ReadCallback(IAsyncResult ar)
    {
        String content = String.Empty;

        //IniFileHelper
        IniFileHelper iniFile = new IniFileHelper();

        // Retrieve the state object and the handler socket  
        // from the asynchronous state object.  
        StateObject state = (StateObject)ar.AsyncState;
        Socket handler = state.workSocket;

        // Read data from the client socket.   
        int bytesRead = handler.EndReceive(ar);  

        if (bytesRead > 0)
        {
            // There  might be more data, so store the data received so far.  
            state.sb.Append(Encoding.ASCII.GetString(
                state.buffer, 0, bytesRead));

            // Check for end-of-file tag. If it is not there, read   
            // more data.  
            content = state.sb.ToString();
            if(content.IndexOf("\r") > -1)
            {
                string ReceivedOutput = null;

                switch (content.Substring(0, 3))
                {
                    case "611":
                        string device1Result = iniFile.ReadValue("Device1", "status", System.IO.Path.GetFullPath("IniFile.ini"));
                        string device2Result = iniFile.ReadValue("Device2", "status", System.IO.Path.GetFullPath("IniFile.ini"));
                        string resultOutput = null;
                        if (device1Result == "-1" && device2Result == "-1")
                        {
                            resultOutput = "0";
                        }
                        else
                        {
                            resultOutput = "1";
                        }

                        ReceivedOutput = "621" + DateTime.Now.ToString("HHmmss") + "015" + "|" + content.Substring(13, 6) + "|" + resultOutput + "|01|02|";

                        if (device1Result != "-1")
                        {
                            bool result = IniFileHelper.WriteValue("Device1", "status", " 1", System.IO.Path.GetFullPath("IniFile.ini"));
                        }
                        if (device2Result != "-1")
                        {
                            bool results = IniFileHelper.WriteValue("Device2", "status", " 1", System.IO.Path.GetFullPath("IniFile.ini"));
                        }
                        break;
                    case "612":
                        string device1Reset = iniFile.ReadValue("Device1", "status", System.IO.Path.GetFullPath("IniFile.ini"));
                        string device2Reset = iniFile.ReadValue("Device2", "status", System.IO.Path.GetFullPath("IniFile.ini"));
                        string resetOutput1 = null;
                        string resetOutput2 = null;
                        if (device1Reset == "-1")
                        {
                            resetOutput1 = "1";
                        }
                        else
                        {
                            resetOutput1 = "0";
                        }

                        if (device2Reset == "-1")
                        {
                            resetOutput2 = "1";
                        }
                        else
                        {
                            resetOutput2 = "0";
                        }
                        ReceivedOutput = "622" + DateTime.Now.ToString("HHmmss") + "010" + "|" + content.Substring(13, 6) + "|" + resetOutput1 + "|" + resetOutput2 + "|";

                        if (device1Reset != "-1")
                        {
                            bool result = IniFileHelper.WriteValue("Device1", "status", "0", System.IO.Path.GetFullPath("IniFile.ini"));
                        }
                        if (device2Reset != "-1")
                        {
                            bool results = IniFileHelper.WriteValue("Device2", "status", "0", System.IO.Path.GetFullPath("IniFile.ini"));
                        }
                        //modbusClient.Connect();
                        //modbusClient.WriteSingleCoil(0001, false);
                        //modbusClient.Disconnect();
                        break;

                    case "619":
                        string device1Status = iniFile.ReadValue("Device1", "status", System.IO.Path.GetFullPath("IniFile.ini"));
                        string device2Status = iniFile.ReadValue("Device2", "status", System.IO.Path.GetFullPath("IniFile.ini"));

                        ReceivedOutput = "629" + DateTime.Now.ToString("HHmmss") + "011" + "|" + content.Substring(13, 6) + "|" + device1Status + "|" + device2Status + "|";
                        break;
                    default:
                        ReceivedOutput = "invalid command code";
                        break;
                }

                // All the data has been read from the   
                // client. Display it on the console.  
                Console.WriteLine("Read {0} bytes from socket.\nData : {1}",
                    content.Length, content);

                // Echo the data back to the client.  
                Send(handler, ReceivedOutput+ "\r");
            }
            else
            {
                // Not all data received. Get more.  
                handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                new AsyncCallback(ReadCallback), state);
            }
        }
    }
    private static void Send(Socket handler, String data)
    {
        // Convert the string data to byte data using ASCII encoding.  
        byte[] byteData = Encoding.ASCII.GetBytes(data);

        // Begin sending the data to the remote device.  
        handler.BeginSend(byteData, 0, byteData.Length, 0,
            new AsyncCallback(SendCallback), handler);
    }
    private static void SendCallback(IAsyncResult ar)
    {
        try
        {
            // Retrieve the socket from the state object.  
            Socket handler = (Socket)ar.AsyncState;

            // Complete sending the data to the remote device.  
            int bytesSent = handler.EndSend(ar);
            Console.WriteLine("Sent {0} bytes to client.", bytesSent);

            handler.Shutdown(SocketShutdown.Both);
            handler.Close();

        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
        }
    }
    public static int Main(String[] args)
    {

        StartListening();
        return 0;
    }

    public static void exitOnPress()
    {
        var key = Console.ReadKey();
        Console.WriteLine("Press esc key to exit ...");
        if (key.Key == ConsoleKey.Escape)
        {
            Environment.Exit(0);
        }
    }
}