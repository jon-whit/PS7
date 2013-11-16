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

        // Queues used to store the SendRequests and ReceiveRequests. These queues
        // will store the messages/payloads being sent, and the callback/payloads
        // for receiving.
        private Queue<ReceiveRequest> ReceiveRequests;
        private Queue<SendRequest> SendRequests;
        private Queue<String> ReceivedMessages;

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
                try
                {
                    // If the SendRequests queue is empty, then queue the message and send it. 
                    if (SendRequests.Count == 0)
                    {
                        SendRequests.Enqueue(new SendRequest(s, (e, o) => callback(e, o), payload));

                        // Decode the string into bytes and use the UnderlyingSocket to send the 
                        // message.
                        Byte[] BytesToBeSent = SocketEncoding.GetBytes(s);
                        UnderlyingSocket.BeginSend(BytesToBeSent, 0, BytesToBeSent.Length, SocketFlags.None,
                            MessageSent, BytesToBeSent);
                    }

                    // If the SendRequests queue is not empty, then enqueue a new Request with the 
                    // given parameters. 
                    else
                        SendRequests.Enqueue(new SendRequest(s, (e, o) => { callback(e, o); }, payload));
                }
                catch (Exception)
                {

                }
            }
        }

        /// <summary>
        /// Callback called upon when the underlying socket has sent a message
        /// successfully. 
        /// </summary>
        private void MessageSent(IAsyncResult result)
        {
            lock (SendLock)
            {
                // Get the bytes that the underlying socket attempted to send.
                byte[] OutgoingBuffer = (byte[]) result.AsyncState;

                // Find out how many bytes were actually sent.
                int BytesSoFar = UnderlyingSocket.EndSend(result);

                if (BytesSoFar == 0)
                {
                    UnderlyingSocket.Close();
                    return;
                }
                // If we have sent the whole message, then dequeue the next SendRequest and call 
                // its callback with the associated exception and payload. 
                else if (BytesSoFar == OutgoingBuffer.Length)
                {
                    // At this point we know that the message has been completely sent. Dequeue the 
                    // next Request.
                    SendRequest CurrentRequest = SendRequests.Dequeue();

                    // Use the ThreadPool to process the callback. 
                    ThreadPool.QueueUserWorkItem(x => CurrentRequest.SendReqCallback(null, CurrentRequest.Payload));

                    // Check if the SendRequests queue contains more requests. If there are more SendRequests,
                    // then peek and send the next message. 
                    if (SendRequests.Count != 0)
                    {
                        SendRequest NextRequest = SendRequests.Peek();
                        Byte[] BytesToBeSent = SocketEncoding.GetBytes(NextRequest.Message);
                        UnderlyingSocket.BeginSend(BytesToBeSent, 0, BytesToBeSent.Length, SocketFlags.None,
                            MessageSent, BytesToBeSent);
                    }

                }
                else
                    // Otherwise, send the remaining bytes by using the offset. 
                    UnderlyingSocket.BeginSend(OutgoingBuffer, BytesSoFar, OutgoingBuffer.Length - BytesSoFar,
                        SocketFlags.None, MessageSent, OutgoingBuffer);
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
                try
                {
                    // An IncomingBuffer for receiving the bytes
                    byte[] IncomingBuffer = new byte[1024];

                    // If the ReceiveRequests queue is empty, then queue the ReceiveRequest
                    // and begin receiving bytes.
                    if (ReceiveRequests.Count == 0)
                    {
                        ReceiveRequests.Enqueue(new ReceiveRequest((s, e, o) => callback(s, e, o), payload));

                        //If there are any strings in ReceivedMessages dequeue the next 
                        //message and execute the next callback.
                        if (ReceivedMessages.Count != 0)
                            ExecuteCallback(ReceivedMessages.Dequeue());

                        // Receive the bytes via the underlying socket.
                        else
                            UnderlyingSocket.BeginReceive(IncomingBuffer, 0, IncomingBuffer.Length, SocketFlags.None,
                                MessageReceived, IncomingBuffer);
                    }

                    // If the ReceiveRequests queue isn't empty, then queue up the ReceiveRequests
                    // so we can process it later.
                    else
                        ReceiveRequests.Enqueue(new ReceiveRequest((s, e, o) => callback(s, e, o), payload));
                }
                catch (Exception)
                {

                }
            }
        }

        /// <summary>
        /// Called when some data has been received.
        /// </summary>
        private void MessageReceived(IAsyncResult result)
        {
            lock (ReceiveLock)
            {
                // Get the buffer to which the data was written.
                byte[] IncomingBuffer = (byte[]) (result.AsyncState);

                // Figure out how many bytes have come in.
                int NumReceivedBytes = UnderlyingSocket.EndReceive(result);

                // If no bytes were received, it means the client closed its side of the socket.
                // Report that to the console and close our socket.
                if (NumReceivedBytes == 0)
                {
                    UnderlyingSocket.Close();
                    return;
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
                        string MessageToProcess = IncomingMessage.Substring(0, index);
                        IncomingMessage = IncomingMessage.Substring(index + 1);

                        // Pass the MessageToProcess to the ExecuteCallBack function so that we can
                        // hand it off to the OS.

                        // If there are pending callbacks, execute the next callback.
                        if (ReceiveRequests.Count != 0)
                            ExecuteCallback(MessageToProcess);

                        // If there are no more pending callbacks add the current string to a queue of strings.
                        else
                        {
                            ReceivedMessages.Enqueue(MessageToProcess);
                        }
                    }

                    if (ReceiveRequests.Count != 0)
                    {
                        // Allow more data to be received from the underlying socket.
                        UnderlyingSocket.BeginReceive(IncomingBuffer, 0, IncomingBuffer.Length, SocketFlags.None,
                            MessageReceived, IncomingBuffer);
                    }
                }
            }
        }

        #region Helper Method(s) for the MessageReceived callback.
        /// <summary>
        /// Dequeues the next ReceiveRequest and passes the callback to the OS so that
        /// it can be handled appropriately.
        /// </summary>
        private void ExecuteCallback(string CurrentMessage)
        {
            // Dequeue the next ReceiveRequest callback and pass it to the ThreadPool to be
            // executed at a later time.
            ReceiveRequest CurrentRequest = ReceiveRequests.Dequeue();

            // Sends the ReceiveRequest callback to the OS to be handled at a later time.
            ThreadPool.QueueUserWorkItem(x => CurrentRequest.RecReqCallback(CurrentMessage, null,
                CurrentRequest.Payload));

       
        }
        #endregion

        #region Nested Classes for Receive and Send Requests

        /// <summary>
        /// Represents a ReceiveRequest for a StringSocket. Each ReceiveRequest contains
        /// a callback and a payload. A ReceiveRequest will be used to store the data
        /// necessary to process messages when they are received across the StringSocket.
        /// </summary>
        private class ReceiveRequest
        {
            public delegate void Callback(string s, Exception e, object payload);

            // Instance variables used to represent a ReceiveRequest:
            private Callback ReceiveCallback;
            private object ReceivePayload;

            // Public Properties so that we have access to the private member
            // variables.
            public Callback RecReqCallback { get { return ReceiveCallback; } }
            public object Payload { get { return ReceivePayload; } }

            /// <summary>
            /// Constructor used to initialize a new ReceiveRequest. A new ReceiveRequest needs
            /// the callback associated with the string, and the payload.
            /// </summary>
            public ReceiveRequest(Callback ReceiveCallback, object Payload)
            {
                this.ReceiveCallback = ReceiveCallback;
                this.ReceivePayload = Payload;
            }
        }

        /// <summary>
        /// Represents a SendRequest for a StringSocket. Each SendRequest contains a
        /// message to be sent, a callback, and some arbitrary payload. A SendRequest
        /// will be used to store the data necessary to process messages when they are
        /// to be sent across the StringSocket.
        /// </summary>
        private class SendRequest
        {
            public delegate void Callback(Exception e, object payload);

            // Instance variables used to represent a SendRequest:
            private string MessageToBeSent;
            private Callback SendCallback;
            private object SendPayload;

            // Public Properties so that we have access to the private member
            // variables.
            public object Payload { get { return SendPayload; } }
            public Callback SendReqCallback { get { return SendCallback; } }
            public string Message { get { return MessageToBeSent; } }

            /// <summary>
            /// Constructor used to initialize a new SendRequest. A new SendRequest needs the
            /// message to be sent, the callback that is associated witht he send request, and
            /// the payload.
            /// </summary>
            public SendRequest(String message, Callback SendCallback, Object payload)
            {
                this.MessageToBeSent = message;
                this.SendCallback = SendCallback;
                this.SendPayload = payload;
            }
        }

        #endregion
    }
}