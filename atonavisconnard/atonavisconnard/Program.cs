using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

class Program
{
    [DllImport("ntdll.dll")]
    public static extern uint NtQuerySystemInformation(
        int SystemInformationClass,
        IntPtr SystemInformation,
        int SystemInformationLength,
        ref int ReturnLength);

    [DllImport("kernel32.dll")]
    public static extern IntPtr OpenProcess(
        uint processAccess,
        bool bInheritHandle,
        int processId);

    [DllImport("kernel32.dll")]
    public static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool DuplicateHandle(
        IntPtr hSourceProcessHandle,
        ushort hSourceHandle,
        IntPtr hTargetProcessHandle,
        out IntPtr lpTargetHandle,
        uint dwDesiredAccess,
        bool bInheritHandle,
        uint dwOptions);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    public static extern uint GetFinalPathNameByHandle(
        IntPtr hFile,
        StringBuilder lpszFilePath,
        uint cchFilePath,
        uint dwFlags);

    const int SystemHandleInformation = 16;
    const uint PROCESS_DUP_HANDLE = 0x0040;
    const uint DUPLICATE_SAME_ACCESS = 0x0002;

    static void Main()
    {
        string target = @"C:\Users\user\AppData\Roaming\Lime3DS\dump\romfs\000400000019AC00\yw2_a.fa".ToLower();

        Console.WriteLine("Analyse du fichier :");
        Console.WriteLine(target);
        Console.WriteLine();

        int length = 0;
        uint status;

        // Première requête pour obtenir la taille
        status = NtQuerySystemInformation(SystemHandleInformation, IntPtr.Zero, 0, ref length);

        if (status != 0xC0000004 && status != 0) // STATUS_INFO_LENGTH_MISMATCH
        {
            Console.WriteLine($"Erreur NTSTATUS inattendue : 0x{status:X}");
            return;
        }

        IntPtr ptr = IntPtr.Zero;

        try
        {
            // Boucle de réallocation si nécessaire
            while (true)
            {
                ptr = Marshal.AllocHGlobal(length);
                status = NtQuerySystemInformation(SystemHandleInformation, ptr, length, ref length);

                if (status == 0) break; // OK

                Marshal.FreeHGlobal(ptr);

                if (status == 0xC0000004) // STATUS_INFO_LENGTH_MISMATCH
                {
                    length *= 2; // on double la taille
                    continue;
                }

                Console.WriteLine($"Erreur lors de la récupération des handles. NTSTATUS = 0x{status:X}");
                return;
            }

            int handleCount = Marshal.ReadInt32(ptr);
            IntPtr handlePtr = ptr + 4;

            bool found = false;

            for (int i = 0; i < handleCount; i++)
            {
                int processId = Marshal.ReadInt32(handlePtr, 4);
                ushort handle = (ushort)Marshal.ReadInt16(handlePtr, 8);

                IntPtr processHandle = OpenProcess(PROCESS_DUP_HANDLE, false, processId);
                if (processHandle != IntPtr.Zero)
                {
                    if (DuplicateHandle(processHandle, handle, Process.GetCurrentProcess().Handle,
                        out IntPtr dupHandle, 0, false, DUPLICATE_SAME_ACCESS))
                    {
                        StringBuilder sb = new StringBuilder(1024);
                        uint result = GetFinalPathNameByHandle(dupHandle, sb, 1024, 0);

                        if (result > 0)
                        {
                            string path = sb.ToString().ToLower();
                            if (path.Contains(target))
                            {
                                try
                                {
                                    Process p = Process.GetProcessById(processId);
                                    Console.WriteLine($"➡ {p.ProcessName} utilise ce fichier");
                                    found = true;
                                }
                                catch { }
                            }
                        }

                        CloseHandle(dupHandle);
                    }

                    CloseHandle(processHandle);
                }

                handlePtr += 16;
            }

            if (!found)
                Console.WriteLine("Aucun logiciel ne semble utiliser ce fichier.");
        }
        finally
        {
            if (ptr != IntPtr.Zero)
                Marshal.FreeHGlobal(ptr);
        }

        Console.WriteLine("\nAnalyse terminée.");
    }
}
