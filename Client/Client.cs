using Shared;
using System;

using System.Net.Sockets;

public class Client
{
    private static readonly SemaphoreSlim respostaRecebida = new SemaphoreSlim(0);
    private static volatile bool terminou;

    private static void Mostrar(string mensagem)
    {
        string[] campos = mensagem.Split('|');
        switch (campos[0])
        {
            case "INICIO":
                Console.WriteLine("Leilão iniciado. Artigo: " + campos[1] +
                    "; preço base: " + campos[2] + "; duração: " + campos[3] + " segundos.");
                break;
            case "ACEITE":
                Console.WriteLine("Licitação aceite. Maior: " + campos[1] + " (" + campos[2] + ").");
                break;
            case "REJEITADA":
                Console.WriteLine("Licitação rejeitada: " + campos[1] +
                    ". Estado atual: " + string.Join(" / ", campos.Skip(2)));
                break;
            case "ESTADO":
                Console.WriteLine("Estado atual: " + string.Join(" / ", campos.Skip(1)));
                break;
            case "FIM":
                Console.WriteLine(campos[1] == "NAO_VENDIDO" ? "Fim: artigo não vendido." :
                    "Fim: vencedor " + campos[2] + ", com " + campos[1] + ".");
                break;
            case "SAIU":
                Console.WriteLine("Saída confirmada.");
                break;
            default:
                Console.WriteLine(mensagem);
                break;
        }
    }

    private static void Receber(StreamReader inReader)
    {
        try
        {
            string? mensagem;
            while ((mensagem = inReader.ReadLine()) != null)
            {
                Mostrar(mensagem);
                // INICIO é um anúncio, não a resposta a um comando do teclado.
                if (mensagem.StartsWith("INICIO|")) continue;
                if (mensagem == "SAIU" || mensagem.StartsWith("FIM|")) break;
                respostaRecebida.Release();
            }
        }
        catch (IOException erro)
        {
            Console.WriteLine("Erro de comunicação: " + erro.Message);
        }
        catch (ObjectDisposedException)
        {
            Console.WriteLine("Ligação fechada.");
        }
        finally
        {
            terminou = true;
            respostaRecebida.Release();
        }
    }

    public static void Main()
    {
        try
        {
            Console.Write("Nome ou IP do servidor: ");
            string hostname = Console.ReadLine() ?? "localhost";
            int portNumber = InputValidation.ValidateIntBetween("Porta (1024 a 65535): ", 1024, 65535);
            using TcpClient clientSocket = new TcpClient(hostname, portNumber);
            NetworkStream stream = clientSocket.GetStream();
            using StreamWriter outWriter = new StreamWriter(stream) { AutoFlush = true };
            using StreamReader inReader = new StreamReader(stream);

            while (true)
            {
                Console.Write("Nome de participante (ou Sair): ");
                string nome = Console.ReadLine()?.Trim() ?? "Sair";
                outWriter.WriteLine(nome.Equals("Sair", StringComparison.OrdinalIgnoreCase) ? "SAIR" : "NOME|" + nome);
                string? resposta = inReader.ReadLine();
                if (resposta == null) return;
                Mostrar(resposta);
                if (resposta == "SAIU" || resposta.EndsWith("ENTRADA_FECHADA")) return;
                if (resposta.StartsWith("NOME_ACEITE|")) break;
            }

            // A receção fica numa Thread, para mostrar INICIO mesmo quando
            // o utilizador está parado no Console.ReadLine e permitir Sair.
            Thread thread = new Thread(() => Receber(inReader));
            thread.Start();
            Console.WriteLine("Comandos: Licitar <valor>, Estado, Sair. Antes do início, apenas Sair.");
            while (true)
            {
                Console.Write("> ");
                string comando = Console.ReadLine()?.Trim() ?? "Sair";
                if (terminou) break;
                if (comando.StartsWith("Licitar ", StringComparison.OrdinalIgnoreCase))
                    comando = "LICITAR|" + comando.Substring(8).Trim();
                outWriter.WriteLine(comando);
                respostaRecebida.Wait();
                if (terminou) break;
            }
            thread.Join();
        }
        catch (SocketException erro)
        {
            Console.WriteLine("Erro de rede no cliente: " + erro.Message);
        }
        catch (IOException erro)
        {
            Console.WriteLine("Erro de entrada/saída: " + erro.Message);
        }
    }
}
