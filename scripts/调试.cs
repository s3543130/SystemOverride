using Godot;
using System;


namespace SystemOverride
{
    internal static class 调试
    {
        internal static void Assert(bool cond, string msg)
#if DEBUG
        {
            if (cond) return;

            GD.PrintErr(msg);
            GD.PushError(msg);
            throw new ApplicationException($"Assert Failed: {msg}");
        }
#else
    {}
#endif
    }
}
