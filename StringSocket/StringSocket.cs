using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Threading;

namespace CustomNetworking
{
    /// <summary> 
    /// A StringSocket is a wrapper around a Socket.  It provides methods that
    /// asynchronously read lines of text (strings terminated by newlines) and 
    /// write strings. (As opposed to Sockets, which read and write raw bytes.)  
    ///
    /// StringSockets are thread safe.  This means that two or more threads may
    /// invoke methods on a shared StringSocket without restriction.  The
    /// StringSocket takes care of the synchonization.
    /// 
    /// Each StringSocket contains a Socket object that is provided by the client.  
    /// A StringSocket will work properly only if the client refrains from calling
    /// the contained Socket's read and write methods.
    /// 
    /// If we have an open Socket s, we can create a StringSocket by doing
    /// 
    ///    StringSocket ss = new StringSocket(s, new UTF8Encoding());
    /// 
    /// We can write a string to the StringSocket by doing
    /// 
    ///    ss.BeginSend("Hello world", callback, payload);
    ///    
    /// where callback is a SendCallback (see below) and payload is an arbitrary object.
    /// This is a non-blocking, asynchronous operation.  When the StringSocket has 
    /// successfully written the string to the underlying Socket, or failed in the 
    /// attempt, it invokes the callback.  The parameters to the callback are a
    /// (possibly null) Exception and the payload.  If the Exception is non-null, it is
    /// the Exception that caused the send attempt to fail.
    /// 
    /// We can read a string from the StringSocket by doing
    /// 
    ///     ss.BeginReceive(callback, payload)
    ///     
    /// where callback is a ReceiveCallback (see below) and payload is an arbitrary object.
    /// This is non-blocking, asynchronous operation.  When the StringSocket has read a
    /// string of text terminated by a newline character from the underlying Socket, or
    /// failed in the attempt, it invokes the callback.  The parameters to the callback are
    /// a (possibly null) string, a (possibly null) Exception, and the payload.  Either the
    /// string or the Exception will be non-null, but nor both.  If the string is non-null, 
    /// it is the requested string (with the newline removed).  If the Exception is non-null, 
    /// it is the Exception that caused the send attempt to fail.
    /// </summary>
    public class StringSocket
    {
        // These delegates describe the callbacks that are used for sending and receiving strings.
        public delegate void SendCallback(Exception e, object payload);
        public delegate void ReceiveCallback(String s, Exception e, object payload);

        // Member variables used to manage this StringSocket:
        private Socket UnderlyingSocket;
        private Encoding SocketEncoding;
        private string OutgoingMessage;
        private string IncomingMessage;
        private readonly object SendLock;
        private readonly object ReceiveLock;
        private bool IsConnected;

        // Queues used to store the SendRequests and ReceiveRequests. These queues
        // will store the messages/payloads being sent, and the callback/payloads
        // for receiving.
        private Queue<ReceiveRequest> ReceiveRequests;
        private Queue<SendRequest> SendRequests;
        private Queue<String> ReceivedMessages;

        public bool Connected {get {return IsConnected;}}
        /// <summary>
        /// Creates a StringSocket from a regular Socket, which should already be connected.  
        /// The read and write methods of the regular Socket must not be called after the
        /// StringSocket is created.  Otherwise, the StringSocket will not behave properly.  
        /// The encoding to use to convert between raw bytes and strings is also provided.
        /// </summary>
        public StringSocket(Socket s, Encoding e)
        {
            this.UnderlyingSocket = s;
            this.SocketEncoding = e;
            this.OutgoingMessage = String.Empty;
            this.IncomingMessage = String.Empty;
            this.SendLock = new object();
            this.ReceiveLock = new object();
            this.IsConnected = true;

            ReceiveRequests = new Queue<ReceiveRequest>();
            SendRequests = new Queue<SendRequest>();
            ReceivedMessages = new Queue<String>();
        }

        /// <summary>
        /// We can write a string to a StringSocket ss by doing
        /// 
        ///    ss.BeginSend("Hello world", callback, payload);
        ///    
        /// where callback is a SendCallback (see below) and payload is an arbitrary object.
        /// This is a non-blocking, asynchronous operation.  When the StringSocket has 
        /// successfully written the string to the underlying Socket, or failed in the 
        /// attempt, it invokes the callback.  The parameters to the callback are a
        /// (possibly null) Exception and the payload.  If the Exception is non-null, it is
        /// the Exception that caused the send attempt to fail. 
        /// 
        /// This method is non-blocking.  This means that it does not wait until the string
        /// has been sent before returning.  Instead, it arranges for the string to be sent
        /// and then returns.  When the send is completed (at some time in the future), the
        /// callback is called on another thread.
        /// 
        /// This method is thread safe.  This means that multiple threads can call BeginSend
        /// on a shared socket without worrying around synchronization.  The implementation of
        /// BeginSend must take care of synchronization instead.  On a given StringSocket, each
        /// string arriving via a BeginSend method call must be sent (in its entirety) before
        /// a later arriving string can be sent.
        /// </summary>
        public void BeginSend(String s, SendCallback callback, object payload)
        {
            lock (SendLock)
            {
                // Enqueue the SendRequest. If the request is the first, start processing
                // the request. Otherwise simply enqueue it and it will be taken care of
                // later.
                SendRequests.Enqueue(new SendRequest { TextToSend = s, Callback = callback, Payload = payload });
                if (SendRequests.Count == 1)
                    ProcessSend();
            }
        }

        /// <summary>
        /// Processes a SendRequest that has been queued. If at any time an exception is thrown, then
        /// that request will be dequeud and returned back to its caller with its appropriate exception 
        /// via the ThreadPool.
        /// </summary>
        private void ProcessSend()
        {
            while (SendRequests.Count > 0)
            {
                // Decode the message to be sent into bytes and use the UnderlyingSocket to send the 
                // message.
                Byte[] BytesToBeSent = SocketEncoding.GetBytes(SendRequests.Peek().TextToSend);
                try
                {
                    UnderlyingSocket.BeginSend(BytesToBeSent, 0, BytesToBeSent.Length, SocketFlags.None,
                        MessageSent, BytesToBeSent);
                    break;
                }

                // If any kind of exception occured, dequeue the SendRequest and hand it off to the
                // OS so that it can be returned approrpriately.
                catch (Exception e)
                {
                    SendRequest CurrentRequest = SendRequests.Dequeue();
                    ThreadPool.QueueUserWorkItem(x => CurrentRequest.Callback(e, CurrentRequest.Payload));
                }
            }
        }

        /// <summary>
        /// Callback called upon when the underlying socket has sent a message
        /// successfully. 
        /// </summary>
        private void MessageSent(IAsyncResult result)
        {
            // Get the bytes that the underlying socket attempted to send.
            byte[] OutgoingBuffer = (byte[]) result.AsyncState;
            int BytesSoFar = 0;
            try
            {
                // Find out how many bytes were actually sent.
                BytesSoFar = UnderlyingSocket.EndSend(result);
            }
            catch (Exception e)
            {
                SendRequest CurrentRequest = SendRequests.Dequeue();
                ThreadPool.QueueUserWorkItem(x => CurrentRequest.Callback(e, CurrentRequest.Payload));
                ProcessSend();
                return;
            }

            // If we have sent the whole message, then dequeue the next SendRequest and call 
            // its callback with the associated exception and payload. 
            if (BytesSoFar == OutgoingBuffer.Length)
            {
                lock (SendLock)
                {
                    // At this point we know that the message has been completely sent. Dequeue the 
                    // next Request.
                    SendRequest CurrentRequest = SendRequests.Dequeue();

                    // Use the ThreadPool to process the callback. 
                    ThreadPool.QueueUserWorkItem(x => CurrentRequest.Callback(null, CurrentRequest.Payload));

                    // Check if the SendRequests queue contains more requests. If there are more SendRequests,
                    // then peek and send the next message. 
                    ProcessSend();
                }
            }
            else
            {
                try
                {
                    // Otherwise, send the remaining bytes by using the offset. 
                    UnderlyingSocket.BeginSend(OutgoingBuffer, BytesSoFar, OutgoingBuffer.Length - BytesSoFar,
                        SocketFlags.None, MessageSent, OutgoingBuffer);
                }
                catch (Exception e)
                {
                    SendRequest CurrentRequest = SendRequests.Dequeue();
                    ThreadPool.QueueUserWorkItem(x => CurrentRequest.Callback(e, CurrentRequest.Payload));
                    ProcessSend();
                }
            }
        }

        /// <summary>
        /// 
        /// <para>
        /// We can read a string from the StringSocket by doing
        /// </para>
        /// 
        /// <para>
        ///     ss.BeginReceive(callback, payload)
        /// </para>
        /// 
        /// <para>
        /// where callback is a ReceiveCallback (see below) and payload is an arbitrary object.
        /// This is non-blocking, asynchronous operation.  When the StringSocket has read a
        /// string of text terminated by a newline character from the underlying Socket, or
        /// failed in the attempt, it invokes the callback.  The parameters to the callback are
        /// a (possibly null) string, a (possibly null) Exception, and the payload.  Either the
        /// string or the Exception will be non-null, but nor both.  If the string is non-null, 
        /// it is the requested string (with the newline removed).  If the Exception is non-null, 
        /// it is the Exception that caused the send attempt to fail.
        /// </para>
        /// 
        /// <para>
        /// This method is non-blocking.  This means that it does not wait until a line of text
        /// has been received before returning.  Instead, it arranges for a line to be received
        /// and then returns.  When the line is actually received (at some time in the future), the
        /// callback is called on another thread.
        /// </para>
        /// 
        /// <para>
        /// This method is thread safe.  This means that multiple threads can call BeginReceive
        /// on a shared socket without worrying around synchronization.  The implementation of
        /// BeginReceive must take care of synchronization instead.  On a given StringSocket, each
        /// arriving line of text must be passed to callbacks in the order in which the corresponding
        /// BeginReceive call arrived.
        /// </para>
        /// 
        /// <para>
        /// Note that it is possible for there to be incoming bytes arriving at the underlying Socket
        /// even when there are no pending callbacks.  StringSocket implementations should refrain
        /// from buffering an unbounded number of incoming bytes beyond what is required to service
        /// the pending callbacks.        
        /// </para>
        /// 
        /// <param name="callback"> The function to call upon receiving the data</param>
        /// <param name="payload"> 
        /// The payload is "remembered" so that when the callback is invoked, it can be associated
        /// with a specific Begin Receiver....
        /// </param>  
        /// 
        /// <example>
        ///   Here is how you might use this code:
        ///   <code>
        ///                    client = new TcpClient("localhost", port);
        ///                    Socket       clientSocket = client.Client;
        ///                    StringSocket receiveSocket = new StringSocket(clientSocket, new UTF8Encoding());
        ///                    receiveSocket.BeginReceive(CompletedReceive1, 1);
        /// 
        ///   </code>
        /// </example>
        /// </summary>
        /// 
        /// 
        public void BeginReceive(ReceiveCallback callback, object payload)
        {
            lock (ReceiveLock)
            {
                ReceiveRequests.Enqueue(new ReceiveRequest{Callback = callback, Payload = payload});
                if (ReceiveRequests.Count == 1)
                    ProcessReceive();
            }
        }

        private void ProcessReceive()
        {
            lock (ReceiveLock)
            {
                // Process each string that has been sent with each ReceiveRequest and hand 
                // the appropriate callback to the OS through the ThreadPool.
                while (ReceivedMessages.Count > 0 && ReceiveRequests.Count > 0)
                {
                    String CurrentMessage = ReceivedMessages.Dequeue();
                    ReceiveRequest CurrentRequest = ReceiveRequests.Dequeue();
                    ThreadPool.QueueUserWorkItem(x => CurrentRequest.Callback(CurrentMessage, null, CurrentRequest.Payload));
                }

                while (ReceiveRequests.Count > 0)
                {
                    try
                    {
                        byte[] IncomingBuffer = new byte[1024];
                        UnderlyingSocket.BeginReceive(IncomingBuffer, 0, IncomingBuffer.Length, SocketFlags.None, MessageReceived, IncomingBuffer);
                        break;
                    }
                    catch (Exception e)
                    {
                        ReceiveRequest CurrentRequest = ReceiveRequests.Dequeue();
                        ThreadPool.QueueUserWorkItem(x => CurrentRequest.Callback(null, e, CurrentRequest.Payload));
                        IncomingMessage = String.Empty;
                    }
                }
            }
        }

        /// <summary>
        /// Called when some data has been received.
        /// </summary>
        private void MessageReceived(IAsyncResult result)
        {
            // Get the buffer to which the data was written.
            byte[] IncomingBuffer = (byte[]) (result.AsyncState);

            // Figure out how many bytes have come in.
            int NumReceivedBytes = 0;
            try
            {
                NumReceivedBytes = UnderlyingSocket.EndReceive(result);
            }
            catch (Exception e)
            {
                ReceiveRequest CurrentRequest = ReceiveRequests.Dequeue();
                ThreadPool.QueueUserWorkItem(x => CurrentRequest.Callback(null, e, CurrentRequest.Payload));
                ProcessReceive();
                IncomingMessage = String.Empty;
                return;
            }

            // If no bytes were received, it means the client closed its side of the socket.
            // Report that to the console and close our socket.
            if (NumReceivedBytes == 0)
            {
                ReceivedMessages.Enqueue(null);
                ProcessReceive();
            }

            // Otherwise, decode the incoming bytes and append them to the IncomingMessage.  
            // Then request more bytes.
            else
            {
                // Convert the bytes into a string. 
                IncomingMessage += SocketEncoding.GetString(IncomingBuffer, 0, NumReceivedBytes);

                // Find the new line character, call the callback function when we find it, and 
                // end receiving. 
                int index;
                while ((index = IncomingMessage.IndexOf('\n')) >= 0)
                {
                    // While there is a newline character, get the 
                    // string up to the newline character.
                    ReceivedMessages.Enqueue(IncomingMessage.Substring(0, index));
                    IncomingMessage = IncomingMessage.Substring(index + 1); 
                }

                ProcessReceive();
            }
        }

        /// <summary>
        /// Closes the underlying socket if it is connected to any hosts. This will finish
        /// any send/receive that may be running at the instant this function is called.
        /// If there are a bulk of sends/receives waiting, then the operation in progress
        /// will be completed and then all other processes will be terminated.
        /// </summary>
        public void Close()
        {
            if (UnderlyingSocket.Connected)
            {
                this.IsConnected = false;
                UnderlyingSocket.Shutdown(SocketShutdown.Both);
                UnderlyingSocket.Close();
            }

        }


        #region Nested Structs for Receive and Send Requests

        /// <summary>
        /// Represents a ReceiveRequest for a StringSocket. Each ReceiveRequest contains
        /// a callback and a payload. A ReceiveRequest will be used to store the data
        /// necessary to process messages when they are received across the StringSocket.
        /// </summary>
        private struct ReceiveRequest
        {
            public ReceiveCallback Callback { get; set; }
            public object Payload { get; set; }
        }

        /// <summary>
        /// Represents a SendRequest for a StringSocket. Each SendRequest contains a
        /// message to be sent, a callback, and some arbitrary payload. A SendRequest
        /// will be used to store the data necessary to process messages when they are
        /// to be sent across the StringSocket.
        /// </summary>
        private struct SendRequest
        {
            public string TextToSend { get; set; }
            public SendCallback Callback { get; set; }
            public object Payload { get; set; }
        }

        #endregion
    }
}