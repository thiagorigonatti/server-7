using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Server7
{

    internal class TelnetClient
    {

        private TcpClient _client;
        private StreamReader _reader;
        private StreamWriter _writer;

        public TelnetClient(string hostname, int port)
        {
            _client = new TcpClient(hostname, port);
            _reader = new StreamReader(_client.GetStream(), Encoding.ASCII);
            _writer = new StreamWriter(_client.GetStream()) { AutoFlush = true };
        }

        public string Read()
        {
            return _reader.ReadLine();
        }

        public void Write(string command)
        {
            _writer.WriteLine(command);
        }

        public void Close()
        {
            _reader.Close();
            _writer.Close();
            _client.Close();
        }


        public void Shutdown7d2dServer(string password)
        {

            Write(password);
            
            Write("shutdown");

            Thread.Sleep(10000);

            Close();

        }
    }
}
