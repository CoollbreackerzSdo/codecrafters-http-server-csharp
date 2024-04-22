using System.Net;
using System.Net.Sockets;
using System.Text;
Console.WriteLine("Logs from your program will appear here!");
// Uncomment this block to pass the first stage
var server = new TcpListener(IPAddress.Any, 4221);
server.Start();

try
{
    while (true) _ = Task.Run(() => Run(server));
}
catch (Exception)
{
    Console.WriteLine("Thread box kill");
    server.Dispose();
}
static ReadOnlySpan<char> ExtractHttpPath(ReadOnlySpan<char> RequestString)
{
    Span<Range> sliceRangePath = new Range[3];
    RequestString.Split(sliceRangePath, ' ', StringSplitOptions.RemoveEmptyEntries);
    return RequestString[sliceRangePath[1]];
}
static ReadOnlySpan<char> ExtractContentPart(ReadOnlySpan<string> RequestString, ReadOnlySpan<char> partSearch)
{
    foreach (ReadOnlySpan<char> header in RequestString)
    {
        if (header.StartsWith(partSearch, StringComparison.InvariantCultureIgnoreCase))
        {
            Span<Range> sliceHeader = new Range[2];
            header.Split(sliceHeader, ' ');
            return header[sliceHeader[1]];
        }
    }
    return [];
}
void Run(TcpListener server)
{
    using Socket socket = server.AcceptSocket();

    var requestBuff = new byte[1024];

    _ = socket.Receive(requestBuff);

    ReadOnlySpan<string> requestParts = Encoding.ASCII.GetString(requestBuff).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

    ReadOnlySpan<char> path = ExtractHttpPath(requestParts[0]);

    Span<byte> responseBuff = new byte[1024];

    ReadOnlySpan<char> matchEcho = "/echo/";
    ReadOnlySpan<char> matchFile = "/files/";

    if (path.SequenceEqual("/"))
    {
        responseBuff = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\n\r\n");
        socket.Send(responseBuff);
        return;
    }
    else if (path.StartsWith(matchEcho, StringComparison.InvariantCultureIgnoreCase))
    {
        ReadOnlySpan<char> contentEcho = path[matchEcho.Length..];
        responseBuff = Encoding.ASCII.GetBytes(
            $"HTTP/1.1 200 OK" +
            $"\r\nContent-Type: text/plain" +
            $"\r\nContent-Length: {contentEcho.Length}" +
            $"\r\n\r\n{contentEcho}");
        socket.Send(responseBuff);
        return;
    }
    else if (path.StartsWith("/user-agent"))
    {
        ReadOnlySpan<char> contentUser = ExtractContentPart(requestParts, "User-Agent:");

        responseBuff = Encoding.ASCII.GetBytes(
             $"HTTP/1.1 200 OK" +
             $"\r\nContent-Type: text/plain" +
             $"\r\nContent-Length: {contentUser.Length}" +
             $"\r\n\r\n{contentUser}");
        socket.Send(responseBuff);
        return;
    }
    else if (path.StartsWith(matchFile))
    {
        string pathFile = Path.Combine(args.Length == 0 ? string.Empty : args[1], new(path[matchFile.Length..]));

        if (requestParts[0].StartsWith("get", StringComparison.InvariantCultureIgnoreCase) && File.Exists(pathFile))
        {
            using var stream = new StreamReader(pathFile);

            Span<char> buffer = new char[stream.BaseStream.Length];
            stream.Read(buffer);

            responseBuff = Encoding.ASCII.GetBytes(
             $"HTTP/1.1 200 OK" +
             $"\r\nContent-Type: application/octet-stream" +
             $"\r\nContent-Length: {buffer.Length}" +
             $"\r\n\r\n{buffer}");

            stream.Close();
            socket.Send(responseBuff);
            return;
        }
        else if (requestParts[0].StartsWith("post", StringComparison.InvariantCultureIgnoreCase))
        {
            ReadOnlySpan<string> contentFile = requestParts[^1].Split('\0', StringSplitOptions.RemoveEmptyEntries);
            using var stream = new StreamWriter(pathFile);

            stream.Write(contentFile[0]);
            stream.Close();

            responseBuff = Encoding.ASCII.GetBytes("HTTP/1.1 201 CREATED\r\n\r\n");
            socket.Send(responseBuff);
            return;
        }
    }
    responseBuff = Encoding.ASCII.GetBytes("HTTP/1.1 404 Not Found\r\n\r\n");
    socket.Send(responseBuff);
    socket.Close();
    return;
}