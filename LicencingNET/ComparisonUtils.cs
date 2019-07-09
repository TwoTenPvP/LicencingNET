namespace LicencingNET
{
    internal static class ComparisonUtils
    {
        internal static bool ConstTimeArrayEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length)
                return false;

            int i = a.Length;
            int cmp = 0;

            while (i != 0)
            {
                --i;
                cmp |= (a[i] ^ b[i]);
            }

            return cmp == 0;
        }
    }
}
