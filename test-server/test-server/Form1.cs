using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Security.Cryptography;
using System.IO;


namespace test_server
{
    public partial class Form1 : Form
    {
        bool terminating = false;
        bool listening = false;
        bool remoteConnected = false;

        Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        Socket remoteSocket;

        List<Socket> socketList = new List<Socket>();
        static List<string> activeClients = new List<string>();
        
        String client_username;
        String code;
        String server_code;
        byte[] randomNum;

        String serverRSAprivate;
        String clientPublic = "";

        int receivedByteLength;

        static string pathToKeys = @"C:\Users\Lenovo\Documents\SABANCI\SeniorYear\CS432\CS432_Project_Spring17\Project\CS432ProjectKeyFiles\";
        String authServerPrivate = pathToKeys + "auth_server_pub_priv.txt";
        String clientPublicKey = "";
        private static RNGCryptoServiceProvider rngCsp = new RNGCryptoServiceProvider();

        int login_no = 1;

        /**STEP 2***/
        byte[] session_key;
        byte[] IV;
        byte[] HMAC_key;

        String t_ticket;
        String authServerPrivateKey;
        String fileServerPublic = pathToKeys + "file_server_pub.txt";
        String fileServerPublicKey;

        public Form1()
        {
            Control.CheckForIllegalCrossThreadCalls = false;
            this.FormClosing += new FormClosingEventHandler(Form1_FormClosing);
            InitializeComponent();
        }

        private void buttonListen_Click(object sender, EventArgs e)
        {

        }

        private String ReadIntoString(String path,String value)
        {
            string text;
            var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);
            using (var streamReader = new StreamReader(fileStream, Encoding.UTF8))
            {
                text = streamReader.ReadToEnd();
                value = text;
            }
            return value;
        }

        private void Accept()
        {
            while (listening)
            {
                try
                {
                    socketList.Add(serverSocket.Accept());
                    textLog.AppendText("A client connected.\n");
                    Thread receiveThread;
                    receiveThread = new Thread(new ThreadStart(Receive));
                    receiveThread.Start();
                }
                catch
                {
                    if (terminating)
                        listening = false;
                    else
                        textLog.AppendText("The socket stopped working.\n");
                }
            }
        }

        private void Receive()
        {
            Socket s = socketList[socketList.Count - 1];
            bool connected = true;

            while (connected && !terminating)
            {
                try
                {
                    
                    Byte[] buffer = new Byte[4096];
                    s.Receive(buffer);
                    string message = Encoding.Default.GetString(buffer);
                    code = message.Substring(0, 3);

                    if (code == "101")
                    {

                        client_username = message.Substring(3, 2);

                        clientPublicKey = pathToKeys + client_username + "_pub.txt";

                        /**8.5.2017*/
                        //ReadIntoString(clientPublicKey,clientPublic);
                       
                        if (activeClients.Contains(client_username))
                        {

                            textLog.AppendText(client_username + " has already been authenticated!\n");

                            byte[] posAck = BitConverter.GetBytes(333);

                            byte[] serverSignature = signWithRSA("333", 4096, serverRSAprivate);

                            byte[] nbuffer = new byte[4 + 128];

                            posAck.CopyTo(nbuffer, 0);
                            serverSignature.CopyTo(nbuffer, 4);

                            s.Send(nbuffer);

                            continue;
                        }


                        textLog.BeginInvoke(
                            (Action)(() =>
                            {
                                textLog.AppendText(Environment.NewLine + "Auth request sent by: " + client_username + " with auth request " + code);
                            }));


                        Byte[] buffer4 = new Byte[64];
                        //Send random number
                        byte[] codeX = Encoding.Default.GetBytes("XXX");
                        codeX.CopyTo(buffer4, 0);
                        randomNum = secureRandGen();
                        randomNum.CopyTo(buffer4, 3); 
                        s.Send(buffer4);
                        textLog.AppendText("Server has sent a challenge: " + generateHexStringFromByteArray(randomNum) + "\n");

                    }
                    else if (code == "102")
                    {
                        String client_sign = message.Substring(3, 32);
                        byte[] client_sign_byte = Encoding.Default.GetBytes(client_sign);
                        textLog.BeginInvoke(
                            (Action)(() =>
                            {
                                textLog.AppendText(Environment.NewLine + "Server acquired signature: " + generateHexStringFromByteArray(client_sign_byte));
                            }));
                        //byte[] randomNumByte = Encoding.Default.GetBytes(randomNum);
                        String c_pub = ReadIntoString(clientPublicKey, clientPublic);
                        bool is_Verified = verifyWithRSA(randomNum, 1024, c_pub, client_sign_byte);

                        int i = 1;
                        if (!is_Verified)
                        {
                            //Send acknowledgment to the client
                            Byte[] buffer5 = new Byte[4];
                            //Send random number
                            byte[] codePos = Encoding.Default.GetBytes("POS");
                            codePos.CopyTo(buffer5, 0);
                            s.Send(buffer5);
                            textLog.AppendText("Server has sent positive ack ");

                        }
                        else
                        {

                        }

                    } else if (code == "REQ")
                    {
                        session_key = secureRandGen();
                        IV = secureRandGen();
                        HMAC_key = secureRandGenForHMAC();
                        String t_sKey = generateHexStringFromByteArray(session_key);
                        String t_IV = generateHexStringFromByteArray(IV);
                        String t_HMAC = generateHexStringFromByteArray(HMAC_key);
                        String t_concat = t_sKey + t_IV + t_HMAC;
                        t_ticket = client_username + t_concat;

                        String auth_server_p_k = ReadIntoString(authServerPrivate,authServerPrivateKey);
                        String file_server_pub_k = ReadIntoString(fileServerPublic, fileServerPublicKey);
                        String c_pub_key = ReadIntoString(clientPublicKey, clientPublic);

                        byte[] signedTicket = signWithRSA(t_ticket, 1024, auth_server_p_k);
                        byte[] encTicket = encryptWithRSA(t_ticket, 1024, c_pub_key);
                        byte[] f_encTicket = encryptWithRSA(t_ticket, 1024, file_server_pub_k);

                        String t_signedTicket = generateHexStringFromByteArray(signedTicket);
                        String t_encTicket = generateHexStringFromByteArray(encTicket);
                        String t_f_encTicket = generateHexStringFromByteArray(f_encTicket);
                        String t_final = "TCT" + t_signedTicket + t_encTicket + t_f_encTicket;

                        byte[] finalTicket = Encoding.Default.GetBytes(t_final);
                        textLog.BeginInvoke(
                            (Action)(() =>
                            {
                                textLog.AppendText(Environment.NewLine + "Server has sent ticket: " + t_final);
                            }));
                        s.Send(finalTicket);
                    }

                }
                catch
                {
                    if (!terminating)
                        textLog.AppendText("A client has disconnected.\n");

                    s.Close();
                    socketList.Remove(s);
                    connected = false;
                }
            }
        }


        // hash function: SHA-256
        static byte[] hashWithSHA256(string input)
        {
            // convert input string to byte array
            byte[] byteInput = Encoding.Default.GetBytes(input);
            // create a hasher object from System.Security.Cryptography
            SHA256CryptoServiceProvider sha256Hasher = new SHA256CryptoServiceProvider();
            // hash and save the resulting byte array
            byte[] result = sha256Hasher.ComputeHash(byteInput);

            return result;
        }

        static byte[] encryptWithRSA(string input, int algoLength, string xmlString)
        {
            // convert input string to byte array
            byte[] byteInput = Encoding.Default.GetBytes(input);
            // create RSA object from System.Security.Cryptography
            RSACryptoServiceProvider rsaObject = new RSACryptoServiceProvider(algoLength);
            // set RSA object with xml string
            rsaObject.FromXmlString(xmlString);
            byte[] result = null;

            try
            {
                result = rsaObject.Encrypt(byteInput, true);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

            return result;
        }

        // signing with RSA
        static byte[] signWithRSA(string input, int algoLength, string xmlString)
        {
            // convert input string to byte array
            byte[] byteInput = Encoding.Default.GetBytes(input);
            // create RSA object from System.Security.Cryptography
            RSACryptoServiceProvider rsaObject = new RSACryptoServiceProvider(algoLength);
            // set RSA object with xml string
            rsaObject.FromXmlString(xmlString);
            byte[] result = null;
            byte[] client_sign = null;

            try
            {
                result = rsaObject.SignData(byteInput, "SHA256");
                String resultText = Encoding.Default.GetString(result);
                client_sign = hashWithSHA256(resultText);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

            return client_sign;
        }



        private void Form1_FormClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (MessageBox.Show("Do you want to exit?", "Server",
                MessageBoxButtons.YesNo) == DialogResult.No)
            {
                e.Cancel = true;
            }
            else
            {
                listening = false;
                terminating = true;
                Environment.Exit(0);
            }
        }

        // verifying with RSA
        static bool verifyWithRSA(byte[] byteInput, int algoLength, string xmlString, byte[] signature)
        {

            // create RSA object from System.Security.Cryptography
            RSACryptoServiceProvider rsaObject = new RSACryptoServiceProvider(algoLength);
            // set RSA object with xml string
            rsaObject.FromXmlString(xmlString);
            bool result = false;

            try
            {
                result = rsaObject.VerifyData(byteInput, "SHA256", signature);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

            return result;
        }

        private void buttonListen_Click_1(object sender, EventArgs e)
        {

        }

        static byte[] GetBytes(string str)
        {
            byte[] bytes = new byte[str.Length * sizeof(char)];
            System.Buffer.BlockCopy(str.ToCharArray(), 0, bytes, 0, bytes.Length);
            return bytes;
        }

        public static byte[] StringToByteArray(String hex)
        {
            int NumberChars = hex.Length;
            byte[] bytes = new byte[NumberChars / 2];
            for (int i = 0; i < NumberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            return bytes;
        }
        static string generateHexStringFromByteArray(byte[] input)
        {
            string hexString = BitConverter.ToString(input);
            return hexString.Replace("-", "");
        }

        private byte[] secureRandGen()
        {

            // Create a byte array to hold the random value. (32B -> 256-bit in this example)
            byte[] randomNumber = new byte[16];

            // Fill the array with a random value.
            rngCsp.GetBytes(randomNumber);

            return randomNumber;
        }

        private byte[] secureRandGenForHMAC()
        {

            // Create a byte array to hold the random value. (32B -> 256-bit in this example)
            byte[] randomNumber = new byte[32];

            // Fill the array with a random value.
            rngCsp.GetBytes(randomNumber);

            return randomNumber;
        }

        private byte[] randGen()
        {
            byte[] rand = new byte[16];
            byte[] number = BitConverter.GetBytes(1234567890334456);
            byte[] number2 = BitConverter.GetBytes(1234567890334456);
            number.CopyTo(rand, 0);
            number2.CopyTo(rand, 7);
            return rand;
        }

        private void buttonListen_Click_2(object sender, EventArgs e)
        {
            int serverPort;
            Thread acceptThread;


            if (Int32.TryParse(textPort.Text, out serverPort))
            {
                serverSocket.Bind(new IPEndPoint(IPAddress.Any, serverPort));
                serverSocket.Listen(5);
                listening = true;
                buttonListen.Enabled = false;
                acceptThread = new Thread(new ThreadStart(Accept));
                acceptThread.Start();

                textLog.AppendText("Started listening on port: " + serverPort + "\n");
            }
            else
            {
                textLog.AppendText("Check port.\n");
            }
        }
    }
}
