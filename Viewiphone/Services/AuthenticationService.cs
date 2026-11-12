namespace Viewiphone.Services
{
    public class AuthenticationService
    {
        public bool ValidateTokenFormat(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return false;
            return token.Length >= 4 && token.Length <= 64;
        }
    }
}
