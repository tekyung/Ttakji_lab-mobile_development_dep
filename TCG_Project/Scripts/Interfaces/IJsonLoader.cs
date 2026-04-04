namespace TCG_Project.Scripts.Interfaces
{
    /// <summary>
    /// 환경(Unity vs Console)에 종속되지 않고 JSON 문자열을 가져오는 인터페이스
    /// </summary>
    public interface IJsonLoader
    {
        string LoadJson(string fileName);
    }
}