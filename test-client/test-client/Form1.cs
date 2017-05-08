using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.IO;
using System.Security.Cryptography;

namespace test_client
{
    public partial class Form1 : Form
    {
        bool terminating = false;
        bool connected = false;

        Socket clientSocket;


        String client_username;
        String client_password;
        String clientRSAprivate;
        String decrypted_clientRSA;
        String clientPublicKey;

        String server_rand_num;

        byte[] decryptedRSA;
        byte[] client_signature;

        int port;
        int user_login_count = 0;

        static string pathToKeys = @"C:\Users\Lenovo\Documents\SABANCI\SeniorYear\CS432\CS432_Project_Spring17\Project\CS432ProjectKeyFiles\";

        static List<Socket> client_Sockets = new List<Socket>();

        string[] enc_priv_Keys = new string[10];
        string[] client_priv_Keys = new string[10];

        /**STEP 2***/
        String server_ticket;

        public Form1()
        {
            fillEncKey();
            fillEncKeyString();
            fillSockets();
            InitializeComponent();
        }

        private void ReadIntoString(int i)
        {
            string text;
            var fileStream = new FileStream(enc_priv_Keys[i], FileMode.Open, FileAccess.Read);
            using (var streamReader = new StreamReader(fileStream, Encoding.UTF8))
            {
                text = streamReader.ReadToEnd();
                client_priv_Keys[i] = text;
            }
            
        }

        private void fillEncKey()
        {
            for(int i=1; i<10; i++)
            {
                enc_priv_Keys[i] = pathToKeys + "enc_c" + i.ToString() + "_pub_priv.txt";
            }

        }

        private void fillEncKeyString()
        {
            for (int i = 1; i < 10; i++)
            {
                ReadIntoString(i);
   
            }
        }

        private void fillSockets()
        {

            /*for (int i = 1; i < 10; i++)
            {
                Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                client_Sockets.Add(socket);
            }*/
            Socket Socket1 = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            Socket Socket2 = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            Socket Socket3 = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            Socket Socket4 = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            Socket Socket5 = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            Socket Socket6 = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            client_Sockets.Add(Socket1);
            client_Sockets.Add(Socket2);
            client_Sockets.Add(Socket3);
            client_Sockets.Add(Socket4);
            client_Sockets.Add(Socket5);
            client_Sockets.Add(Socket6);
        }

        static byte[] decryptWithAES128(byte[] byteInput, byte[] key, byte[] IV)
        {
            // convert input string to byte array
            //byte[] byteInput = Encoding.Default.GetBytes(input);

            // create AES object from System.Security.Cryptography
            RijndaelManaged aesObject = new RijndaelManaged();
            // since we want to use AES-128
            aesObject.KeySize = 128;
            // block size of AES is 128 bits
            aesObject.BlockSize = 128;
            // mode -> CipherMode.*
            aesObject.Mode = CipherMode.CFB;
            // feedback size should be equal to block size
            aesObject.FeedbackSize = 128;
            // set the key
            aesObject.Key = key;
            // set the IV
            aesObject.IV = IV;

            // create an encryptor with the settings provided
            ICryptoTransform decryptor = aesObject.CreateDecryptor();
            byte[] result = null;

            try
            {
                result = decryptor.TransformFinalBlock(byteInput, 0, byteInput.Length);
            }
            catch (Exception e) // if encryption fails
            {
                Console.WriteLine(e.Message); // display the cause
            }

            return result;
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


        // helper functions
        static string generateHexStringFromByteArray(byte[] input)
        {
            string hexString = BitConverter.ToString(input);
            return hexString.Replace("-", "");
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

        private bool checkPassword(string cpassword, int i,string encKey)
        {
            try
            {
                //Hash the password and parse the byte array to get key and IV values
                byte[] key = new byte[16];
                byte[] IV = new byte[16];
                byte[] hashedpw = hashWithSHA256(cpassword);

                textLog.AppendText("Hashed password! \n");
                //textLog.AppendText(generateHexStringFromByteArray(hashedpw) + "\n");

                Array.Copy(hashedpw, 0, key, 0, 16);
                Array.Copy(hashedpw, 16, IV, 0, 16);

                byte[] encKeyByte = StringToByteArray(encKey);

                //Decrypt the server RSA key with the hashed password
                byte[] decryptedRSAkey = decryptWithAES128(encKeyByte, key, IV);
                String decryptedRSAkeyText = Encoding.Default.GetString(decryptedRSAkey);


                textLog.AppendText("AES128 Decryption executed! \n");
                //textLog.AppendText(Encoding.Default.GetString(decryptedRSAkey) + "\n");

                if (decryptedRSAkey != null && decryptedRSAkeyText.Contains("<RSAKeyValue>"))
                {
                    decrypted_clientRSA = Encoding.Default.GetString(decryptedRSAkey);
                    return true;
                }
            }
            catch
            {

                textLog.AppendText("Password incorrect! Please try again! \n");
                username.Clear();
                password.Clear();

            }

            return false;
        }

        private void Form1_FormClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (MessageBox.Show("Do you want to exit?", "Client",
                MessageBoxButtons.YesNo) == DialogResult.No)
            {
                e.Cancel = true;
            }
            else
            {
                connected = false;
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

        private void Receive()
        {
            while (connected)
            {
                try
                {
                    //Receive the challenge sent by the server
                    Byte[] buffer = new Byte[67];
                    clientSocket.Receive(buffer);

                    string message = Encoding.Default.GetString(buffer);
                    string server_code = message.Substring(0, 3);

                    if (server_code == "XXX")
                    {

                        server_rand_num = message.Substring(3, 16);
                        byte[] randG = Encoding.Default.GetBytes(server_rand_num);
                        textLog.BeginInvoke(
                                   (Action)(() =>
                                   {
                                       textLog.AppendText(Environment.NewLine + generateHexStringFromByteArray(randG));
                                   }));

                        Byte[] buffer2 = new Byte[64];
                        client_signature = signWithRSA(server_rand_num, 2048, decrypted_clientRSA);
                        textLog.BeginInvoke(
                                   (Action)(() =>
                                   {
                                       textLog.AppendText(Environment.NewLine + "Client signature: " + generateHexStringFromByteArray(client_signature));
                                   }));
                        String sign = Encoding.Default.GetString(client_signature);
                        buffer2 = Encoding.Default.GetBytes("102" + sign);
                        try
                        {
                            clientSocket.Send(buffer2);
                            /*textLog.BeginInvoke(
                                   (Action)(() =>
                                   {
                                       textLog.AppendText(Environment.NewLine + "Signature sent with challenge: " + server_rand_num);
                                   }));*/

                        }
                        catch (Exception ex)
                        {
                            textLog.BeginInvoke(
                                   (Action)(() =>
                                   {
                                       textLog.AppendText(Environment.NewLine + "Signature cannot be sent " + server_rand_num);
                                   }));
                            Console.WriteLine(ex.ToString());
                        }

                    }
                    else if (server_code == "POS")
                    {
                        Byte[] buffer3 = new Byte[10];
                        buffer3 = Encoding.Default.GetBytes("REQ");
                        clientSocket.Send(buffer3);

                    }
                    else if(server_code == "TCT")
                    {
                        server_ticket = message.Substring(3, 16);  //DEGISECEK
                        //byte[] ticketByte = Encoding.Default.GetBytes(server_ticket);
                        textLog.BeginInvoke(
                                   (Action)(() =>
                                   {
                                       textLog.AppendText(Environment.NewLine +"Client has received access ticket: " + server_ticket);
                                   }));
                    }


                }
                catch
                {
                    if (!terminating)
                    {
                        textLog.AppendText("Lost connection to server.\n");
                        buttonConnect.Enabled = true;
                    }
                    connected = false;
                    clientSocket.Close();
                    clientSocket = null;
                }
            }
        }

        private void SendSignature()
        {
            Byte[] buffer = new Byte[64];
            client_signature = signWithRSA(server_rand_num, 1024, decrypted_clientRSA);  //DEĞİŞTİR!!!!
            String sign = Encoding.Default.GetString(client_signature);
            buffer = Encoding.Default.GetBytes("102" + sign);

            try
            {
                clientSocket.Send(buffer);
                textLog.BeginInvoke(
                       (Action)(() =>
                       {
                           textLog.AppendText(Environment.NewLine + "Signature sent" + server_rand_num);
                       }));

            }
            catch (Exception ex)
            {
                textLog.BeginInvoke(
                       (Action)(() =>
                       {
                           textLog.AppendText(Environment.NewLine + "Signature cannot be sent " + server_rand_num);
                       }));
                Console.WriteLine(ex.ToString());
            }

        }

        private void ReceiveAck()
        {
            while (connected)
            {
                try
                {
                    //Receive the challenge sent by the server
                    Byte[] buffer = new Byte[32];
                    clientSocket.Receive(buffer);


                }
                catch
                {
                    if (!terminating)
                    {
                        textLog.AppendText("Lost connection to server.\n");
                        buttonConnect.Enabled = true;
                    }
                    connected = false;
                    clientSocket.Close();
                    clientSocket = null;
                }
            }

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

        private void textIp_TextChanged(object sender, EventArgs e)
        {

        }

        private void buttonConnect_Click_1(object sender, EventArgs e)
        {
            clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            String IP = textIp.Text;
            int port;
            client_username = username.Text;
            client_password = password.Text;
            int client_no = Int32.Parse(client_username.Substring(1, 1));

            String encryptedFileName = enc_priv_Keys[client_no];

            if (!File.Exists(encryptedFileName)) // check if user name is a valid one
            {
                textLog.AppendText("Wrong User Name!\n");
                username.Clear();
                password.Clear();
            }
            else
            {

                if (Int32.TryParse(textPort.Text, out port))
                {
                   
                    try
                    {
                        bool is_password_correct = checkPassword(client_password, client_no, client_priv_Keys[client_no]);
                        

                        if (is_password_correct)
                        {
                            //client_Sockets.ElementAt<Socket>(user_login_count).Connect(IP, port);
                            clientSocket.Connect(IP, port);
                            connected = true;
                            buttonConnect.Enabled = false;

                            //SEND REQUEST
                            Byte[] buffer = new Byte[64];
                            buffer = Encoding.Default.GetBytes("101" + client_username);
                            clientSocket.Send(buffer);

                            Thread receiveThread;
                            receiveThread = new Thread(new ThreadStart(Receive));
                            receiveThread.Start();
                            textLog.AppendText("Client no:" + client_no.ToString() + " has connected to server.\n");
                            buttonConnect.Enabled = true;
                        }
                        else
                        {
                            connected = false;
                            terminating = true;
                            MessageBox.Show("Password is incorrect! Please try again");
                            username.Clear();
                            password.Clear();
                        }

                    }
                    catch
                    {

                        textLog.AppendText("Could not connect.\n");
                    }
                }
                else
                {
                    textLog.AppendText("Check port.");
                }

            }
        }
    }
}
