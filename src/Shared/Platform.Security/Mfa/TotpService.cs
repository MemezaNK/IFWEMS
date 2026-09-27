using System.Security.Cryptography;
using System.Text;

namespace Platform.Security.Mfa;

/// <summary>
/// RFC 6238 time-based one-time passwords (30-second step, 6 digits, HMAC-SHA1) — the algorithm
/// used by Microsoft/Google Authenticator and similar apps. Used for SEC-002 multi-factor
/// authentication without a dependency on an external identity provider.
/// </summary>
public interface ITotpService
{
    string GenerateSecret();
    string BuildOtpAuthUri(string issuer, string accountName, string base32Secret);
    bool Verify(string base32Secret, string code, DateTime utcNow, int allowedDriftSteps = 1);
    string ComputeCode(string base32Secret, DateTime utcNow);
}

public sealed class TotpService : ITotpService
{
    private const int StepSeconds = 30;
    private const int Digits = 6;

    public string GenerateSecret() => Base32.Encode(RandomNumberGenerator.GetBytes(20));

    public string BuildOtpAuthUri(string issuer, string accountName, string base32Secret) =>
        $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(accountName)}" +
        $"?secret={base32Secret}&issuer={Uri.EscapeDataString(issuer)}&algorithm=SHA1&digits={Digits}&period={StepSeconds}";

    public bool Verify(string base32Secret, string code, DateTime utcNow, int allowedDriftSteps = 1)
    {
        if (string.IsNullOrWhiteSpace(code)) return false;
        code = code.Trim().Replace(" ", string.Empty);
        if (code.Length != Digits || !code.All(char.IsDigit)) return false;

        var key = Base32.Decode(base32Secret);
        var counter = ToCounter(utcNow);
        for (var drift = -allowedDriftSteps; drift <= allowedDriftSteps; drift++)
        {
            var expected = Compute(key, counter + drift);
            if (CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(code)))
            {
                return true;
            }
        }
        return false;
    }

    public string ComputeCode(string base32Secret, DateTime utcNow) => Compute(Base32.Decode(base32Secret), ToCounter(utcNow));

    private static long ToCounter(DateTime utcNow) =>
        new DateTimeOffset(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc)).ToUnixTimeSeconds() / StepSeconds;

    private static string Compute(byte[] key, long counter)
    {
        var counterBytes = BitConverter.GetBytes(counter);
        if (BitConverter.IsLittleEndian) Array.Reverse(counterBytes);

        using var hmac = new HMACSHA1(key);
        var hash = hmac.ComputeHash(counterBytes);
        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24)
                     | (hash[offset + 1] << 16)
                     | (hash[offset + 2] << 8)
                     | hash[offset + 3];
        var otp = binary % (int)Math.Pow(10, Digits);
        return otp.ToString(new string('0', Digits));
    }
}

/// <summary>RFC 4648 base32 (no padding), as used in otpauth:// URIs.</summary>
public static class Base32
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static string Encode(byte[] data)
    {
        var sb = new StringBuilder((data.Length * 8 + 4) / 5);
        int buffer = 0, bitsLeft = 0;
        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bitsLeft += 8;
            while (bitsLeft >= 5)
            {
                sb.Append(Alphabet[(buffer >> (bitsLeft - 5)) & 31]);
                bitsLeft -= 5;
            }
        }
        if (bitsLeft > 0) sb.Append(Alphabet[(buffer << (5 - bitsLeft)) & 31]);
        return sb.ToString();
    }

    public static byte[] Decode(string input)
    {
        input = input.Trim().TrimEnd('=').Replace(" ", string.Empty).ToUpperInvariant();
        var output = new List<byte>(input.Length * 5 / 8);
        int buffer = 0, bitsLeft = 0;
        foreach (var c in input)
        {
            var value = Alphabet.IndexOf(c);
            if (value < 0) throw new FormatException("Invalid base32 character.");
            buffer = (buffer << 5) | value;
            bitsLeft += 5;
            if (bitsLeft >= 8)
            {
                output.Add((byte)((buffer >> (bitsLeft - 8)) & 0xFF));
                bitsLeft -= 8;
            }
        }
        return output.ToArray();
    }
}
