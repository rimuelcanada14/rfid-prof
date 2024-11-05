using System.Threading.Tasks;

public interface IRFIDService
{
    Task<string> ReadRFIDAsync();
}
