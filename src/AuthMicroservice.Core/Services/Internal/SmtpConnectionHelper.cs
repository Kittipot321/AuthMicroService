using AuthMicroservice.Core.Configuration;
using MailKit.Security;

namespace AuthMicroservice.Core.Services.Internal;

internal static class SmtpConnectionHelper
{
    public static SecureSocketOptions ResolveSecureSocketOption(SmtpOptions smtp)
    {
        if (smtp.UseSsl)
        {
            return SecureSocketOptions.SslOnConnect;
        }
        if (smtp.UseStartTls)
        {
            return SecureSocketOptions.StartTls;
        }
        return SecureSocketOptions.Auto;
    }
}
