using CustomNetworking;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;

namespace StringSocketTester
{

    /// <summary>
    ///This is a test class for StringSocketTest and is intended
    ///to contain all StringSocketTest Unit Tests
    ///</summary>
    [TestClass()]
    public class StringSocketTest
    {


        private TestContext testContextInstance;

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext
        {
            get
            {
                return testContextInstance;
            }
            set
            {
                testContextInstance = value;
            }
        }

        /// <summary>
        ///A simple test for BeginSend and BeginReceive
        ///</summary>
        [TestMethod()]
        public void Test1()
        {
            new Test1Class().run(4001);
        }

        public class Test1Class
        {
            // Data that is shared across threads
            private ManualResetEvent mre1;
            private ManualResetEvent mre2;
            private ManualResetEvent mre3;
            private ManualResetEvent mre4;
            private String s1;
            private object p1;
            private String s2;
            private object p2;
            private String s3;
            private object p3;
            private String s4;
            private object p4;

            // Timeout used in test case
            private static int timeout = 2000;

            public void run(int port)
            {
                // Create and start a server and client.
                TcpListener server = null;
                TcpClient client = null;

                try
                {
                    server = new TcpListener(IPAddress.Any, port);
                    server.Start();
                    client = new TcpClient("localhost", port);

                    // Obtain the sockets from the two ends of the connection.  We are using the blocking AcceptSocket()
                    // method here, which is OK for a test case.
                    Socket serverSocket = server.AcceptSocket();
                    Socket clientSocket = client.Client;

                    // Wrap the two ends of the connection into StringSockets
                    StringSocket sendSocket = new StringSocket(serverSocket, new UTF8Encoding());
                    StringSocket receiveSocket = new StringSocket(clientSocket, new UTF8Encoding());

                    // This will coordinate communication between the threads of the test cases
                    mre1 = new ManualResetEvent(false);
                    mre2 = new ManualResetEvent(false);
                    mre3 = new ManualResetEvent(false);
                    mre4 = new ManualResetEvent(false);
                    
                    // Make two receive requests
                    receiveSocket.BeginReceive(CompletedReceive1, 1);
                    receiveSocket.BeginReceive(CompletedReceive2, 2);
                    receiveSocket.BeginReceive(CompletedReceive3, 3);

                    // Now send the data.  Hope those receive requests didn't block!
                    String msg = "Hello World\nThis is a Test\nIts Working!\n";

                    foreach (char c in msg)
                    {
                        sendSocket.BeginSend(c.ToString(), (e, o) => { }, null);
                    }

                    // Make sure the lines were received properly.
                    Assert.AreEqual(true, mre1.WaitOne(timeout), "Timed out waiting 1");
                    Assert.AreEqual("Hello World", s1);
                    Assert.AreEqual(1, p1);

                    Assert.AreEqual(true, mre2.WaitOne(timeout), "Timed out waiting 2");
                    Assert.AreEqual("This is a Test", s2);
                    Assert.AreEqual(2, p2);

                    Assert.AreEqual(true, mre3.WaitOne(timeout), "Timed out waiting 3");
                    Assert.AreEqual("Its Working!", s3);
                    Assert.AreEqual(3, p3);
                    
                    receiveSocket.BeginReceive(CompletedReceive4, 4);

                    sendSocket.BeginSend("First Message\n", (e, o) => { }, null);
                    sendSocket.BeginSend("Second Message\n", (e, o) => { }, null);

                    Assert.AreEqual(true, mre4.WaitOne(timeout), "Timed out waiting 4");
                    Assert.AreEqual("First Message", s4);
                    Assert.AreEqual(4, p4);
                }
                finally
                {
                    server.Stop();
                    client.Close();
                }
            }

            // This is the callback for the first receive request.  We can't make assertions anywhere
            // but the main thread, so we write the values to member variables so they can be tested
            // on the main thread.
            private void CompletedReceive1(String s, Exception o, object payload)
            {
                s1 = s;
                p1 = payload;
                mre1.Set();
            }

            // This is the callback for the second receive request.
            private void CompletedReceive2(String s, Exception o, object payload)
            {
                s2 = s;
                p2 = payload;
                mre2.Set();
            }

            // This is the callback for the third receive request.
            private void CompletedReceive3(String s, Exception o, object payload)
            {
                s3 = s;
                p3 = payload;
                mre3.Set();
            }

            // This is the callback for the fourth receive request.
            private void CompletedReceive4(String s, Exception o, object payload)
            {
                s4 = s;
                p4 = payload;
                mre4.Set();
            }
        }

        /// <summary>
        /// This method is from Tatyana Beall. This is a simple test of sending 3 strings, 
        /// where only one of them has "\n". This is to check that the received string is a 
        /// right combination of the strings.
        /// </summary>
        [TestMethod()]
        public void Test2()
        {
            new Test2Class().run(4001);
        }

        public class Test2Class
        {
            // Data that is shared across threads
            private ManualResetEvent mre1;

            private String s1;
            private object p1;


            // Timeout used in test case
            private static int timeout = 2000;

            public void run(int port)
            {
                // Create and start a server and client.
                TcpListener server = null;
                TcpClient client = null;

                try
                {
                    server = new TcpListener(IPAddress.Any, port);
                    server.Start();
                    client = new TcpClient("localhost", port);

                    // Obtain the sockets from the two ends of the connection.  We are using the blocking AcceptSocket()
                    // method here, which is OK for a test case.
                    Socket serverSocket = server.AcceptSocket();
                    Socket clientSocket = client.Client;

                    // Wrap the two ends of the connection into StringSockets
                    StringSocket sendSocket = new StringSocket(serverSocket, new UTF8Encoding());
                    StringSocket receiveSocket = new StringSocket(clientSocket, new UTF8Encoding());

                    // This will coordinate communication between the threads of the test cases
                    mre1 = new ManualResetEvent(false);


                    // Make one receive request
                    receiveSocket.BeginReceive(CompletedReceive1, 1);


                    // Now send the data. 

                    sendSocket.BeginSend("Slow ", (e, o) => { }, null);
                    sendSocket.BeginSend("and steady ", (e, o) => { }, null);
                    sendSocket.BeginSend("wins the race!\n123\n", (e, o) => { }, null);

                    // Make sure the lines were received properly.
                    Assert.AreEqual(true, mre1.WaitOne(timeout), "Timed out waiting 1");
                    Assert.AreEqual("Slow and steady wins the race!", s1);
                    Assert.AreEqual(1, p1);


                }
                finally
                {
                    server.Stop();
                    client.Close();
                }
            }

            // This is the callback for the first receive request.  We can't make assertions anywhere
            // but the main thread, so we write the values to member variables so they can be tested
            // on the main thread.
            private void CompletedReceive1(String s, Exception o, object payload)
            {
                s1 = s;
                p1 = payload;
                mre1.Set();
            }


        }

        /*Daniel Houston 00671205 and Jonathan Whitaker 00752100 Test Case*/

        [TestMethod]
        public void GradingTest()
        {
            new GradingTestClass().run();
        }

        [TestClass]
        private class GradingTestClass
        {
            String s1 = null;
            object p1 = null;
            String s2 = null;
            object p2 = null;
            String s3 = null;
            object p3 = null;

            // Create and start a server and client.
            TcpListener server = null;
            TcpClient client1 = null;

            //Manual Reset Events used to give the test some time. 
            ManualResetEvent mre1 = new ManualResetEvent(false);
            ManualResetEvent mre2 = new ManualResetEvent(false);
            ManualResetEvent mre3 = new ManualResetEvent(false);

            int timeout = 2000;

            public void run()
            {
                try
                {
                    server = new TcpListener(IPAddress.Any, 5000);
                    server.Start();
                    client1 = new TcpClient("localhost", 5000);

                    // Obtain the sockets from the two ends of the connection.  We are using the blocking AcceptSocket()
                    // method here, which is OK for a test case.
                    Socket serverSocket = server.AcceptSocket();
                    Socket clientSocket1 = client1.Client;

                    // Wrap the two ends of the connection into StringSockets
                    StringSocket sendSocket = new StringSocket(serverSocket, new UTF8Encoding());
                    StringSocket receiveSocket1 = new StringSocket(clientSocket1, new UTF8Encoding());

                    // Make two receive requests
                    receiveSocket1.BeginReceive(CompletedReceive1, 1);
                    receiveSocket1.BeginReceive(CompletedReceive2, 2);
                    receiveSocket1.BeginReceive(CompletedReceive3, 3);

                    // Now send the data.  Hope those receive requests didn't block!
                    String msg1 = "Hello World 1\nHello World 2\nHello World 3\n";

                    sendSocket.BeginSend(msg1, (e, o) => { }, null);
                    
                    // Make sure the lines were received properly.
                    Assert.AreEqual(true, mre1.WaitOne(timeout), "Timed out waiting 1");
                    Assert.AreEqual("Hello World 1", s1);
                    Assert.AreEqual(1, p1);

                    Assert.AreEqual(true, mre2.WaitOne(timeout), "Timed out waiting 2");
                    Assert.AreEqual("Hello World 2", s2);
                    Assert.AreEqual(2, p2);

                    Assert.AreEqual(true, mre3.WaitOne(timeout), "Timed out waiting 3");
                    Assert.AreEqual("Hello World 3", s3);
                    Assert.AreEqual(3, p3);

                    mre1 = new ManualResetEvent(false);
                    mre2 = new ManualResetEvent(false);
                    mre3 = new ManualResetEvent(false);

                    // Make one receive request
                    receiveSocket1.BeginReceive(CompletedReceive1, 4);

                    // Now send the data.
                    msg1 = "Hello\nWorld\nHi\n";

                    sendSocket.BeginSend(msg1, (e, o) => { }, null);

                    //Thread.Sleep(2000);

                    // Send two more BeginReceive requests and the two remaining strings
                    // from the BeginSend should be waiting for us. 
                    receiveSocket1.BeginReceive(CompletedReceive2, 5);
                    receiveSocket1.BeginReceive(CompletedReceive3, 6);

                    // Make sure the lines were received properly.
                    Assert.AreEqual(true, mre1.WaitOne(timeout), "Timed out waiting 1");
                    Assert.AreEqual("Hello", s1);
                    Assert.AreEqual(4, p1);

                    Assert.AreEqual(true, mre2.WaitOne(timeout), "Timed out waiting 2");
                    Assert.AreEqual("World", s2);
                    Assert.AreEqual(5, p2);

                    Assert.AreEqual(true, mre3.WaitOne(timeout), "Timed out waiting 3");
                    Assert.AreEqual("Hi", s3);
                    Assert.AreEqual(6, p3);

                    mre1 = new ManualResetEvent(false);
                    mre2 = new ManualResetEvent(false);
                    mre3 = new ManualResetEvent(false);

                    // First send the data.
                    msg1 = "Jon\nDan\n!\n";

                    sendSocket.BeginSend(msg1, (e, o) => { }, null);

                    // Send two more BeginReceive requests and the two remaining strings
                    // from the BeginSend should be waiting for us. 
                    // Make one receive request
                    receiveSocket1.BeginReceive(CompletedReceive1, 7);
                    receiveSocket1.BeginReceive(CompletedReceive2, 8);
                    receiveSocket1.BeginReceive(CompletedReceive3, 9);

                    // Make sure the lines were received properly.
                    Assert.AreEqual(true, mre1.WaitOne(timeout), "Timed out waiting 1");
                    Assert.AreEqual("Jon", s1);
                    Assert.AreEqual(7, p1);

                    Assert.AreEqual(true, mre2.WaitOne(timeout), "Timed out waiting 2");
                    Assert.AreEqual("Dan", s2);
                    Assert.AreEqual(8, p2);

                    Assert.AreEqual(true, mre3.WaitOne(timeout), "Timed out waiting 3");
                    Assert.AreEqual("!", s3);
                    Assert.AreEqual(9, p3);

                }
                finally
                {
                    server.Stop();
                    client1.Close();
                }
            }

            private void CompletedReceive1(String s, Exception o, object payload)
            {
                s1 = s;
                p1 = payload;
                mre1.Set();
            }

            private void CompletedReceive2(String s, Exception o, object payload)
            {
                s2 = s;
                p2 = payload;
                mre2.Set();
            }

            private void CompletedReceive3(String s, Exception o, object payload)
            {
                s3 = s;
                p3 = payload;
                mre3.Set();
            }
        }
       
        [TestMethod()]
        public void Test3()
        {
            new OnlineTest1Class().run(4001);
        }

        public class OnlineTest1Class
        {
            // Data that is shared across threads
            private ManualResetEvent mre1;
            private ManualResetEvent mre2;
            private String s1;
            private object p1;
            private String s2;
            private object p2;

            // Timeout used in test case
            private static int timeout = 2000;
            private static string longString= "1234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890";

            public void run(int port)
            {
                // Create and start a server and client.
                TcpListener server = null;
                TcpClient client = null;

                try
                {
                    server = new TcpListener(IPAddress.Any, port);
                    server.Start();
                    client = new TcpClient("localhost", port);

                    // Obtain the sockets from the two ends of the connection.  We are using the blocking AcceptSocket()
                    // method here, which is OK for a test case.
                    Socket serverSocket = server.AcceptSocket();
                    Socket clientSocket = client.Client;

                    // Wrap the two ends of the connection into StringSockets
                    StringSocket sendSocket = new StringSocket(serverSocket, new UTF8Encoding());
                    StringSocket receiveSocket = new StringSocket(clientSocket, new UTF8Encoding());

                    // This will coordinate communication between the threads of the test cases
                    mre1 = new ManualResetEvent(false);

                    // Make receive request
                    receiveSocket.BeginReceive(CompletedReceive1, 1);

                    // Now send the data.  Hope those receive requests didn't block!
                    sendSocket.BeginSend(longString + "\n", (e, o) => { }, null);
                    //}
                    Thread.Sleep(3000);
                    // Make sure the lines were received properly.
                    Assert.AreEqual(true, mre1.WaitOne(timeout), "Timed out waiting 1");
                    Assert.AreEqual(longString, s1);
                    Assert.AreEqual(1, p1);

                }
                finally
                {
                    server.Stop();
                    client.Close();
                }
            }

            // This is the callback for the first receive request.  We can't make assertions anywhere
            // but the main thread, so we write the values to member variables so they can be tested
            // on the main thread.
            private void CompletedReceive1(String s, Exception o, object payload)
            {
                s1 = s;
                p1 = payload;
                mre1.Set();
            }
        }

        [TestMethod]
        public void MultipleThreadTest1()
        {
            new HVTestClass().run(4000);
        }

        public class HVTestClass
        {
            // Data that is shared across threads
            private ManualResetEvent mre1;
            private ManualResetEvent mre2;
            private String s1;
            private object p1;
            private String s2;
            private object p2;

            // Timeout used in test case
            private static int timeout = 120000;

            public void run(int port)
            {
                // Create and start a server and client.
                TcpListener server = null;
                TcpClient client = null;

                try
                {
                    server = new TcpListener(IPAddress.Any, port);
                    server.Start();
                    client = new TcpClient("localhost", port);

                    // Obtain the sockets from the two ends of the connection.  We are using the blocking AcceptSocket()
                    // method here, which is OK for a test case.
                    Socket serverSocket = server.AcceptSocket();
                    Socket clientSocket = client.Client;

                    // Wrap the two ends of the connection into StringSockets
                    StringSocket sendSocket = new StringSocket(serverSocket, new UTF8Encoding());
                    StringSocket receiveSocket = new StringSocket(clientSocket, new UTF8Encoding());

                    // This will coordinate communication between the threads of the test cases
                    mre1 = new ManualResetEvent(false);
                    mre2 = new ManualResetEvent(false);

                    // Make two receive requests
                    receiveSocket.BeginReceive(CompletedReceive1, 1);
                    receiveSocket.BeginReceive(CompletedReceive2, 2);


                    // Now send the data.  Hope those receive requests didn't block!
                    String msg1 = "The FIRST string\n";
                    work w = new work(sendSocket, msg1, 1);
                    ThreadStart threadDelegate = new ThreadStart(w.sendOnThread);
                    Thread Thread1 = new Thread(threadDelegate);


                    String msg2 = "The SECOND string\n";
                    work w2 = new work(sendSocket, msg2, 2);
                    ThreadStart threadDelegate2 = new ThreadStart(w2.sendOnThread);
                    Thread Thread2 = new Thread(threadDelegate2);

                    Thread1.Start();
                    Thread2.Start();

                    // Make sure the lines were received properly.
                    Assert.AreEqual(true, mre1.WaitOne(timeout), "Timed out waiting 1");
                    Assert.AreEqual("The FIRST string", s1);
                    Assert.AreEqual(1, p1);

                    Assert.AreEqual(true, mre2.WaitOne(timeout), "Timed out waiting 2");
                    Assert.AreEqual("The SECOND string", s2);
                    Assert.AreEqual(2, p2);
                }
                finally
                {
                    server.Stop();
                    client.Close();
                }
            }

            // This is the callback for the first receive request.  We can't make assertions anywhere
            // but the main thread, so we write the values to member variables so they can be tested
            // on the main thread.
            private void CompletedReceive1(String s, Exception o, object payload)
            {
                s1 = s;
                p1 = payload;
                mre1.Set();
            }

            // This is the callback for the second receive request.
            private void CompletedReceive2(String s, Exception o, object payload)
            {
                s2 = s;
                p2 = payload;
                mre2.Set();
            }
        }

        public class work
        {
            private StringSocket _sendSocket;
            private string _message;
            private object _payload;
            public work(StringSocket soc, string mess, object pyld)
            {
                this._sendSocket = soc;
                this._message = mess;
                this._payload = pyld;
            }
            public void sendOnThread()
            {
                _sendSocket.BeginSend(this._message, (e, o) => { }, this._payload);
            }
        }

        
        /// <summary>
        /// Brandon Koch and Dalton Wallace
        /// </summary>
        [TestMethod()]
        public void RealCoolTest()
        {
            //new RealCoolTestClass().run(4001);
        }

        
        public class RealCoolTestClass
        {
            // Data that is shared across threads
            private ManualResetEvent mre1;


            private String receivedMessage;


            private object p1;
            private object p2;
            private object p3;
            private object p4;
            private object p5;
            private object p6;
            private object p7;
            private object p8;
            private object p9;
            private object p10;
            private object p11;


            // Timeout used in test case
            private static int timeout = 1200000;

            public void run(int port)
            {
                // Create and start a server and client.
                TcpListener server = null;
                TcpClient client = null;

                try
                {
                    server = new TcpListener(IPAddress.Any, port);
                    server.Start();
                    client = new TcpClient("localhost", port);

                    // Obtain the sockets from the two ends of the connection.  We are using the blocking AcceptSocket()
                    // method here, which is OK for a test case.
                    Socket serverSocket = server.AcceptSocket();
                    Socket clientSocket = client.Client;

                    // Wrap the two ends of the connection into StringSockets
                    StringSocket sendSocket = new StringSocket(serverSocket, new UTF8Encoding());
                    StringSocket receiveSocket = new StringSocket(clientSocket, new UTF8Encoding());

                    // This will coordinate communication between the threads of the test cases
                    mre1 = new ManualResetEvent(false);

                    // Make 11 receive request
                    receiveSocket.BeginReceive(PoemReceived1, 1);
                    receiveSocket.BeginReceive(PoemReceived2, 2);
                    receiveSocket.BeginReceive(PoemReceived3, 3);
                    receiveSocket.BeginReceive(PoemReceived4, 4);
                    receiveSocket.BeginReceive(PoemReceived5, 5);
                    receiveSocket.BeginReceive(PoemReceived6, 6);
                    receiveSocket.BeginReceive(PoemReceived7, 7);
                    receiveSocket.BeginReceive(PoemReceived8, 8);
                    receiveSocket.BeginReceive(PoemReceived9, 9);
                    receiveSocket.BeginReceive(PoemReceived10, 10);
                    receiveSocket.BeginReceive(PoemReceived11, 11);

                    string sendMessage = "'THE POOL PLAYERS. \n SEVEN AT THE GOLDEN SHOVEL.' \n by Gwendolyn Brooks \n We real cool. We \n Left school. We \n Lurk late. We \n Strike straight. We \n Sing sin. We \n Thin gin. We \n Jazz June. We \n Die soon.\n";

                    sendSocket.BeginSend(sendMessage, (e, o) => { }, null);

                    // Make sure the lines were received properly.
                    Assert.AreEqual(true, mre1.WaitOne(timeout), "Timed out waiting 11");
                    Assert.AreEqual(sendMessage, receivedMessage);
                    Assert.AreEqual(11, p11);

                }
                finally
                {
                    server.Stop();
                    client.Close();
                }
            }

            // This is the callback for the first receive request.  We can't make assertions anywhere
            // but the main thread, so we write the values to member variables so they can be tested
            // on the main thread.
            private void PoemReceived1(String s, Exception o, object payload)
            {
                receivedMessage = receivedMessage + s + "\n";
                p1 = payload;
                
            }

            private void PoemReceived2(String s, Exception o, object payload)
            {
                receivedMessage = receivedMessage + s + "\n";
                p2 = payload;
                
            }

            private void PoemReceived3(String s, Exception o, object payload)
            {
                receivedMessage = receivedMessage + s + "\n";
                p3 = payload;
                
            }

            private void PoemReceived4(String s, Exception o, object payload)
            {
                receivedMessage = receivedMessage + s + "\n";
                p4 = payload;
                
            }

            private void PoemReceived5(String s, Exception o, object payload)
            {
                receivedMessage = receivedMessage + s + "\n";
                p5 = payload;
                
            }

            private void PoemReceived6(String s, Exception o, object payload)
            {
                receivedMessage = receivedMessage + s + "\n";
                p6 = payload;
                
            }

            private void PoemReceived7(String s, Exception o, object payload)
            {
                receivedMessage = receivedMessage + s + "\n";
                p7 = payload;
                
            }

            private void PoemReceived8(String s, Exception o, object payload)
            {
                receivedMessage = receivedMessage + s + "\n";
                p8 = payload;
                
            }

            private void PoemReceived9(String s, Exception o, object payload)
            {
                receivedMessage = receivedMessage + s + "\n";
                p9 = payload;
                
            }

            private void PoemReceived10(String s, Exception o, object payload)
            {
                receivedMessage = receivedMessage + s + "\n";
                p10 = payload;
                
            }

            private void PoemReceived11(String s, Exception o, object payload)
            {
                receivedMessage = receivedMessage + s + "\n";
                p11 = payload;
                mre1.Set();
            }


        }

        /// <summary>
        ///A simple test to make sure that two sentences that are 200 words long are being sent and received
        ///Jeongun Yu and Lucia Paredes
        ///</summary>
        [TestMethod()]
        public void JeongunTest()
        {
            new Test4Class().run(4001);
        }

        public class Test4Class
        {
            // Data that is shared across threads
            private ManualResetEvent mre1;
            private ManualResetEvent mre2;
            private String s1;
            private object p1;
            private String s2;
            private object p2;

            // Timeout used in test case
            private static int timeout = 2000;

            public void run(int port)
            {
                // Create and start a server and client.
                TcpListener server = null;
                TcpClient client = null;

                try
                {
                    server = new TcpListener(IPAddress.Any, port);
                    server.Start();
                    client = new TcpClient("localhost", port);

                    // Obtain the sockets from the two ends of the connection.  We are using the blocking AcceptSocket()
                    // method here, which is OK for a test case.
                    Socket serverSocket = server.AcceptSocket();
                    Socket clientSocket = client.Client;

                    // Wrap the two ends of the connection into StringSockets
                    StringSocket sendSocket = new StringSocket(serverSocket, new UTF8Encoding());
                    StringSocket receiveSocket = new StringSocket(clientSocket, new UTF8Encoding());

                    // This will coordinate communication between the threads of the test cases
                    mre1 = new ManualResetEvent(false);
                    mre2 = new ManualResetEvent(false);

                    // Make two receive requests
                    receiveSocket.BeginReceive(CompletedReceive1, 1);
                    receiveSocket.BeginReceive(CompletedReceive2, 2);

                    List<string> sentences = new List<string>();
                    string[] words = { "?", "wagstaff", "nicol", "the", "for",
	    "and", "a", "with", "naruto", "fox", "23", "mao" };
                    for (int i = 0; i < 2; i++)
                    {
                        RandomText text = new RandomText(words);
                        text.AddContentParagraphs(1, 2, 4, 200, 200);
                        sentences.Add(text.Content);
                    }
                    String msg = sentences[0] + "\n" + sentences[1] + "\n";

                    foreach (char c in msg)
                    {
                        sendSocket.BeginSend(c.ToString(), (e, o) => { }, null);
                    }

                    // Make sure the lines were received properly.
                    Assert.AreEqual(true, mre1.WaitOne(timeout), "Timed out waiting 1");
                    Assert.AreEqual(sentences[0], s1);
                    Assert.AreEqual(1, p1);

                    Assert.AreEqual(true, mre2.WaitOne(timeout), "Timed out waiting 2");
                    Assert.AreEqual(sentences[1], s2);
                    Assert.AreEqual(2, p2);
                }
                finally
                {
                    server.Stop();
                    client.Close();
                }
            }

            // This is the callback for the first receive request.  We can't make assertions anywhere
            // but the main thread, so we write the values to member variables so they can be tested
            // on the main thread.
            private void CompletedReceive1(String s, Exception o, object payload)
            {
                s1 = s;
                p1 = payload;
                mre1.Set();
            }

            // This is the callback for the second receive request.
            private void CompletedReceive2(String s, Exception o, object payload)
            {
                s2 = s;
                p2 = payload;
                mre2.Set();
            }
        }

        public class RandomText
        {
            static Random _random = new Random();
            StringBuilder _builder;
            string[] _words;

            public RandomText(string[] words)
            {
                _builder = new StringBuilder();
                _words = words;
            }

            public void AddContentParagraphs(int numberParagraphs, int minSentences,
            int maxSentences, int minWords, int maxWords)
            {
                for (int i = 0; i < numberParagraphs; i++)
                {
                    AddParagraph(_random.Next(minSentences, maxSentences + 1),
                         minWords, maxWords);
                    _builder.Append("");
                }
            }

            void AddParagraph(int numberSentences, int minWords, int maxWords)
            {
                for (int i = 0; i < numberSentences; i++)
                {
                    int count = _random.Next(minWords, maxWords + 1);
                    AddSentence(count);
                }
            }

            void AddSentence(int numberWords)
            {
                StringBuilder b = new StringBuilder();
                // Add n words together.
                for (int i = 0; i < numberWords; i++) // Number of words
                {
                    b.Append(_words[_random.Next(_words.Length)]).Append(" ");
                }
                string sentence = b.ToString().Trim() + ". ";
                // Uppercase sentence
                sentence = char.ToUpper(sentence[0]) + sentence.Substring(1);
                // Add this sentence to the class
                _builder.Append(sentence);
            }

            public string Content
            {
                get
                {
                    return _builder.ToString();
                }
            }
        }

        /// <summary>
        ///Roy Bastien and Andraia Allsop
        ///
        /// This test repeatedly sends "Hello " 100 times before a newline \n is sent. 
        /// After the message is received, equality is asserted.
        ///</summary>
        [TestMethod()]
        public void Test5()
        {
            new Test5Class().run(4001);
        }

        public class Test5Class
        {
            // Data that is shared across threads
            private ManualResetEvent mre1;
            private String s1;
            private object p1;

            // Timeout used in test case
            private static int timeout = 2000;

            public void run(int port)
            {
                // Create and start a server and client.
                TcpListener server = null;
                TcpClient client = null;

                try
                {
                    server = new TcpListener(IPAddress.Any, port);
                    server.Start();
                    client = new TcpClient("localhost", port);

                    // Obtain the sockets from the two ends of the connection.  We are using the blocking AcceptSocket()
                    // method here, which is OK for a test case.
                    Socket serverSocket = server.AcceptSocket();
                    Socket clientSocket = client.Client;

                    // Wrap the two ends of the connection into StringSockets
                    StringSocket sendSocket = new StringSocket(serverSocket, new UTF8Encoding());
                    StringSocket receiveSocket = new StringSocket(clientSocket, new UTF8Encoding());

                    // This will coordinate communication between the threads of the test cases
                    mre1 = new ManualResetEvent(false);

                    // Make a receive request
                    receiveSocket.BeginReceive(CompletedReceive, 1);

                    String msg = "Hello ";

                    String sentMessage = System.String.Empty;
                    for (int i = 0; i < 100; i++)
                    {
                        sendSocket.BeginSend(msg, (e, o) => { }, null);

                        // building the message to be asserted
                        sentMessage = sentMessage + msg;
                    }

                    sendSocket.BeginSend("\n", (e, o) => { }, null);


                    // Make sure the line was received properly.
                    Assert.AreEqual(true, mre1.WaitOne(timeout), "Timed out waiting 1");
                    Assert.AreEqual(sentMessage, s1);
                    Assert.AreEqual(1, p1);
                }
                finally
                {
                    server.Stop();
                    client.Close();
                }
            }

            // This is the callback for the receive request.  We can't make assertions anywhere
            // but the main thread, so we write the values to member variables so they can be tested
            // on the main thread.
            private void CompletedReceive(String s, Exception o, object payload)
            {
                s1 = s;
                p1 = payload;
                mre1.Set();
            }
        }

        /// <summary>
        ///A simple test for BeginSend and BeginReceive
        ///</summary>
        [TestMethod()]
        public void SendNReceiveMediumVolume()
        {
            for (int i = 0; i < 10; i++)
            {
                new MediumLoadTestClassJaredandGeoff().run(4001);
            }
        }


        public class MediumLoadTestClassJaredandGeoff
        {
            private const int depth = 10;
            private List<string> receivedMessages = new List<string>();
            private int receivedCount = 0;
            private readonly object lockObj = new object();
            // Data that is shared across threads
            private ManualResetEvent mre1;


            // Timeout used in test case
            private static int timeout = 20000;

            public void run(int port)
            {
                // Create and start a server and client.
                TcpListener server = null;
                TcpClient client = null;

                try
                {
                    server = new TcpListener(IPAddress.Any, port);
                    server.Start();
                    client = new TcpClient("localhost", port);

                    // Obtain the sockets from the two ends of the connection. We are using the blocking AcceptSocket()
                    // method here, which is OK for a test case.
                    Socket serverSocket = server.AcceptSocket();
                    Socket clientSocket = client.Client;

                    // Wrap the two ends of the connection into StringSockets
                    StringSocket sendSocket = new StringSocket(serverSocket, new UTF8Encoding());
                    StringSocket receiveSocket = new StringSocket(clientSocket, new UTF8Encoding());

                    // This will coordinate communication between the threads of the test cases
                    mre1 = new ManualResetEvent(false);

                    //receive each number from 0 to depth
                    for (int i = 0; i < depth; i++)
                    {
                        receiveSocket.BeginReceive(ReceiveCallback1, i); // Make a receive request
                    }
                    // send each number from 0 to depth
                    for (int i = 0; i < depth; i++)
                    {
                        sendSocket.BeginSend(i.ToString() + "\n", (e, o) => { }, i); // Make a send request
                    }
                    // Make sure we got all the responses
                    for (int i = 0; i < depth; i++)
                    {
                        Assert.AreEqual(true, mre1.WaitOne(timeout), "Timed out waiting");
                        Assert.IsTrue(receivedMessages.Count != 0);
                        Assert.IsTrue(receivedMessages.Contains(i.ToString()), "Missed message on " + i);
                    }
                }
                finally
                {
                    server.Stop();
                    client.Close();
                }
            }

            private void ReceiveCallback1(string s, Exception e, object payload)
            {
                Assert.IsFalse(s == null);
                lock (lockObj)
                {
                    receivedMessages.Add(s); // Save messages in the order that they are received
                    receivedCount++;
                }
                Assert.AreEqual(payload.ToString(), s);
                if (receivedCount == depth)
                    mre1.Set(); // Set for timeout
            }
        }

        /// <summary>
        ///A simple stress test for BeginSend and BeginReceive.
        ///Sends many integers in succession that should come out the other
        ///end of the sausage tube in order.
        ///</summary>
        [TestMethod()]
        public void IntStressTest()
        {
            new StressTestClass().run(4001);
        }

        public class StressTestClass
        {
            // Data that is shared across threads
            private ManualResetEvent mre1;
            private String s1;
            private object p1;
            private int order;
            bool ordered;

            public void run(int port)
            {
                // Create and start a server and client.
                TcpListener server = null;
                TcpClient client = null;

                try
                {
                    server = new TcpListener(IPAddress.Any, port);
                    server.Start();
                    client = new TcpClient("localhost", port);

                    // Obtain the sockets from the two ends of the connection.  We are using the blocking AcceptSocket()
                    // method here, which is OK for a test case.
                    Socket serverSocket = server.AcceptSocket();
                    Socket clientSocket = client.Client;

                    // Wrap the two ends of the connection into StringSockets
                    StringSocket sendSocket = new StringSocket(serverSocket, new UTF8Encoding());
                    StringSocket receiveSocket = new StringSocket(clientSocket, new UTF8Encoding());

                    // This will coordinate communication between the threads of the test cases
                    mre1 = new ManualResetEvent(false);

                    // how many messages will be sent with \n
                    int how_many_messages = 1000;
                    order = 0;
                    ordered = true;

                    // Make n receive requests
                    for (int i = 0; i < how_many_messages; i++)
                    {
                        receiveSocket.BeginReceive(CompletedReceiveOfInt, i);
                    }

                    // make a ridiculously long string with many numbers in order followed by returns
                    String msg = "";


                    for (int i = 0; i < how_many_messages; i++)
                    {
                        msg += i + "\\n";
                    }

                    foreach (char c in msg)
                    {
                        sendSocket.BeginSend(c.ToString(), (e, o) => { }, null);
                    }

                    // Make sure the lines were received properly.
                    Assert.IsTrue(ordered);

                }
                finally
                {
                    server.Stop();
                    client.Close();
                }
            }


            /// <summary>
            /// Delegate method that checks the ordering of the int received by the sender.
            /// If the order is wrong, a global flag is set to false, causing the assertion
            /// in main to fail.
            /// 
            /// Also checks that the payload is in the correct order, as main uses int as the payload object.
            /// </summary>
            /// <param name="s"></param>
            /// <param name="o"></param>
            /// <param name="payload"></param>
            private void CompletedReceiveOfInt(String s, Exception o, object payload)
            {
                int parsed;
                bool is_ordered = false;
                int payload_to_int = (Int32)payload;

                if (Int32.TryParse(s, out parsed))
                {
                    is_ordered = (order == parsed && order == payload_to_int);
                }

                if (ordered && !is_ordered)
                {
                    ordered = false;
                }

                order++;
            }
        }

        //Written by Kelley Schaefer
        //This test should make sure that the Haiku is received in proper order
        [TestMethod()]
        public void Haiku()
        {
            new HaikuClass().run(4001);
        }

        public class HaikuClass
        {
            // Data that is shared across threads
            private ManualResetEvent mre1;
            private ManualResetEvent mre2;
            private ManualResetEvent mre3;
            private String str1;
            private object obj1;
            private String str2;
            private object obj2;
            private String str3;
            private object obj3;

            // Timeout used in test case
            private static int timeout = 2000;
            private static string test_string = "I got Second Wind\nMetaphorically Speaking\nBreath Overrated.";


            public void run(int port)
            {
                // Create and start a server and client.
                TcpListener server = null;
                TcpClient client = null;
                try
                {
                    server = new TcpListener(IPAddress.Any, port);
                    server.Start();
                    client = new TcpClient("localhost", port);

                    // Obtain the sockets from the two ends of the connection.  We are using the blocking AcceptSocket()
                    // method here, which is OK for a test case.
                    Socket serverSocket = server.AcceptSocket();
                    Socket clientSocket = client.Client;

                    // Wrap the two ends of the connection into StringSockets
                    StringSocket sendSocket = new StringSocket(serverSocket, new UTF8Encoding());
                    StringSocket receiveSocket = new StringSocket(clientSocket, new UTF8Encoding());

                    // This will coordinate communication between the threads of the test cases
                    mre1 = new ManualResetEvent(false);
                    mre2 = new ManualResetEvent(false);
                    mre3 = new ManualResetEvent(false);

                    // Make receive request
                    receiveSocket.BeginReceive(CompletedReceive1, 1);
                    receiveSocket.BeginReceive(CompletedReceive2, 2);
                    receiveSocket.BeginReceive(CompletedReceive3, 3);

                    sendSocket.BeginSend(test_string + "\n", (e, o) => { }, null);

                    // Make sure the lines were received properly.
                    Assert.AreEqual(true, mre1.WaitOne(timeout), "Timed out waiting 1");
                    Assert.AreEqual("I got Second Wind", str1);
                    Assert.AreEqual(1, obj1);

                    Assert.AreEqual(true, mre2.WaitOne(timeout), "Timed out waiting 2");
                    Assert.AreEqual("Metaphorically Speaking", str2);
                    Assert.AreEqual(2, obj2);

                    Assert.AreEqual(true, mre3.WaitOne(timeout), "Timed out waiting 3");
                    Assert.AreEqual("Breath Overrated.", str3);
                    Assert.AreEqual(3, obj3);

                }
                finally
                {
                    server.Stop();
                    client.Close();
                }
            }

            // This is the callback for the first receive request.  We can't make assertions anywhere
            // but the main thread, so we write the values to member variables so they can be tested
            // on the main thread.
            private void CompletedReceive1(String s, Exception o, object payload)
            {
                str1 = s;
                obj1 = payload;
                mre1.Set();
            }
            private void CompletedReceive2(String s, Exception o, object payload)
            {
                str2 = s;
                obj2 = payload;
                mre2.Set();
            }
            private void CompletedReceive3(String s, Exception o, object payload)
            {
                str3 = s;
                obj3 = payload;
                mre3.Set();
            }
        }

        /// <summary>
        /// William Shupe
        /// 
        /// A test to see if the StringSocket will continue to execute even if there is a long callback.
        /// The messages should be received in a certain order because one of the callbacks
        /// will execute very slowly, allowing the second one to finish first.
        ///</summary>
        [TestMethod()]
        public void LongCallbackTest()
        {
            new LongCallbackClass().run(4001);
        }

        public class LongCallbackClass
        {
            // Data that is shared across threads
            private ManualResetEvent mre1;
            private ManualResetEvent mre2;
            private String s1;
            private object p1;
            private String s2;
            private object p2;

            // Timeout used in test case
            private static int timeout = 7000;

            public void run(int port)
            {
                // Create and start a server and client.
                TcpListener server = null;
                TcpClient client = null;
                s1 = "";
                try
                {
                    server = new TcpListener(IPAddress.Any, port);
                    server.Start();
                    client = new TcpClient("localhost", port);

                    // Obtain the sockets from the two ends of the connection.  We are using the blocking AcceptSocket()
                    // method here, which is OK for a test case.
                    Socket serverSocket = server.AcceptSocket();
                    Socket clientSocket = client.Client;

                    // Wrap the two ends of the connection into StringSockets
                    StringSocket sendSocket = new StringSocket(serverSocket, new UTF8Encoding());
                    StringSocket receiveSocket = new StringSocket(clientSocket, new UTF8Encoding());

                    // This will coordinate communication between the threads of the test cases
                    mre1 = new ManualResetEvent(false);
                    mre2 = new ManualResetEvent(false);

                    // Make two receive requests
                    receiveSocket.BeginReceive(CompletedReceive1, 1);
                    receiveSocket.BeginReceive(CompletedReceive2, 2);

                    // Now send the data.  Hope those receive requests didn't block!
                    String msg = "Hello world\nThis is a test\n";
                    foreach (char c in msg)
                    {
                        sendSocket.BeginSend(c.ToString(), (e, o) => { }, null);
                    }

                    // Make sure the lines were received properly.
                    Assert.AreEqual(true, mre2.WaitOne(timeout), "Timed out waiting 2");
                    Assert.AreEqual(2, p2);

                    Assert.AreEqual(true, mre1.WaitOne(timeout), "Timed out waiting 1");
                    Assert.AreEqual(1, p1);

                    Assert.AreEqual("This is a testHello world", s1);
                }
                finally
                {
                    server.Stop();
                    client.Close();
                }
            }

            // This is the callback for the first receive request.  
            // We sleep the thread to simulate a long callback function
            private void CompletedReceive1(String s, Exception o, object payload)
            {
                //Pause the thread for five seconds to simulate a long callback function
                Thread.Sleep(5000);
                lock (s1)
                {
                    s1 += s;
                }
                p1 = payload;
                mre1.Set();

            }

            // This is the callback for the second receive request.
            // This callback should execute quickly
            private void CompletedReceive2(String s, Exception o, object payload)
            {
                lock (s1)
                {
                    s1 += s;
                }
                p2 = payload;
                mre2.Set();

            }
        }


    }
}

