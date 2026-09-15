using System.Text;

namespace SSLConfiguration.Application
{
    /// <summary>
    /// Same as old SSLConfiguration_CommonUtility.UniqueKey (PAC username/password).
    /// </summary>
    public static class UniqueKey
    {
        public static int RandomNumber(int min, int max) => Random.Shared.Next(min, max);

        public static string RandomString(int size, bool lowerCase)
        {
            var builder = new StringBuilder(size);
            for (int i = 0; i < size; i++)
            {
                char ch = Convert.ToChar(Convert.ToInt32(Math.Floor(26 * Random.Shared.NextDouble() + 65)));
                builder.Append(ch);
            }

            return lowerCase ? builder.ToString().ToLowerInvariant() : builder.ToString();
        }

        public static string RandomPassword()
        {
            var builder = new StringBuilder();
            builder.Append(RandomString(8, false));
            builder.Append(RandomNumber(100, 999));
            builder.Append(RandomString(4, true));
            builder.Append(RandomNumber(10, 50));
            return builder.ToString();
        }
    }
}
