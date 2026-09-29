// Project-specific native bridge test.
// Pattern: Unity documentation, "Create a native plug-in for iOS".

extern "C"
{
    int INBridge_GetVersion()
    {
        return 1;
    }
}