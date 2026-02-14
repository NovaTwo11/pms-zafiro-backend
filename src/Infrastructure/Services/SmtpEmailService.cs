using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using PmsZafiro.Application.Interfaces;

namespace PmsZafiro.Infrastructure.Services;

public class SmtpEmailService : IEmailService
{
    private readonly IConfiguration _config;

    public SmtpEmailService(IConfiguration config)
    {
        _config = config;
    }

    public async Task SendEmailAsync(string to, string subject, string body, bool isHtml = true)
    {
        var host = _config["Smtp:Host"];
        // Modo simulación si no has configurado las variables en appsettings.json
        if (string.IsNullOrEmpty(host))
        {
            Console.WriteLine($"[EMAIL MOCK] Para: {to} | Asunto: {subject} \nCuerpo: {body}");
            return;
        }

        var port = int.Parse(_config["Smtp:Port"] ?? "587");
        var user = _config["Smtp:User"];
        var pass = _config["Smtp:Pass"];
        var from = _config["Smtp:From"] ?? "noreply@pmszafiro.com";

        using var client = new SmtpClient(host, port)
        {
            Credentials = new NetworkCredential(user, pass),
            EnableSsl = true
        };

        var mailMessage = new MailMessage
        {
            From = new MailAddress(from, "PMS Zafiro"),
            Subject = subject,
            Body = body,
            IsBodyHtml = isHtml,
        };
        mailMessage.To.Add(to);

        await client.SendMailAsync(mailMessage);
    }
}