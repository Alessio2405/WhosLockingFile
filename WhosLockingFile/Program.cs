using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
class Program
{
	static void Main()
	{
		Console.Write("Inserisci path completo: ");
		string filePath = Console.ReadLine();

		if (string.IsNullOrWhiteSpace(filePath))
		{
			Console.WriteLine("Path non valido.");
			return;
		}

		var lockingProcesses = GetLockingProcesses(filePath);

		if (lockingProcesses.Count > 0)
		{
			Console.WriteLine("Processi che bloccano il file:");
			foreach (var proc in lockingProcesses)
			{
				Console.WriteLine($"PID: {proc.Id}, Name: {proc.ProcessName}");
			}
		}
		else
		{
			Console.WriteLine("Nessun processo sta bloccando il file.");
		}
	}

	static List<Process> GetLockingProcesses(string filePath)
	{
		List<Process> lockingProcesses = new();
		uint sessionHandle = 0;

		int result = RmStartSession(out sessionHandle, 0, Guid.NewGuid().ToString());
		if (result != 0)
			return lockingProcesses;

		try
		{
			string[] resources = new string[] { filePath };
			result = RmRegisterResources(sessionHandle, (uint)resources.Length, resources, 0, 0, 0, 0);
			if (result != 0)
				return lockingProcesses;

			uint pnProcInfoNeeded = 0, pnProcInfo = 0, lpdwRebootReasons = 0;
			result = RmGetList(sessionHandle, out pnProcInfoNeeded, ref pnProcInfo, null, ref lpdwRebootReasons);

			if (result == 234) 
			{
				RM_PROCESS_INFO[] processInfo = new RM_PROCESS_INFO[pnProcInfoNeeded];
				pnProcInfo = pnProcInfoNeeded;
				result = RmGetList(sessionHandle, out pnProcInfoNeeded, ref pnProcInfo, processInfo, ref lpdwRebootReasons);

				if (result == 0)
				{
					foreach (var info in processInfo)
					{
						try
						{
							Process process = Process.GetProcessById(info.Process.dwProcessId);
							lockingProcesses.Add(process);
						}
						catch { }
					}
				}
			}
		}
		finally
		{
			RmEndSession(sessionHandle);
		}

		return lockingProcesses;
	}

	#region Native Methods

	private const int RmRebootReasonNone = 0;
	private const int CCH_RM_MAX_APP_NAME = 255;
	private const int CCH_RM_MAX_SVC_NAME = 63;

	[StructLayout(LayoutKind.Sequential)]
	private struct RM_UNIQUE_PROCESS
	{
		public int dwProcessId;
		public FILETIME ProcessStartTime;
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	private struct RM_PROCESS_INFO
	{
		public RM_UNIQUE_PROCESS Process;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCH_RM_MAX_APP_NAME + 1)]
		public string strAppName;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCH_RM_MAX_SVC_NAME + 1)]
		public string strServiceShortName;
		public uint ApplicationType;
		public uint AppStatus;
		public uint TSSessionId;
		[MarshalAs(UnmanagedType.Bool)]
		public bool bRestartable;
	}

	[DllImport("rstrtmgr.dll", CharSet = CharSet.Auto)]
	private static extern int RmStartSession(out uint pSessionHandle, int dwSessionFlags, string strSessionKey);

	[DllImport("rstrtmgr.dll", CharSet = CharSet.Auto)]
	private static extern int RmEndSession(uint pSessionHandle);

	[DllImport("rstrtmgr.dll", CharSet = CharSet.Auto)]
	private static extern int RmRegisterResources(uint pSessionHandle, uint nFiles, string[] rgsFilenames,
		uint nApplications, IntPtr rgApplications, uint nServices, IntPtr rgsServiceNames);

	[DllImport("rstrtmgr.dll", CharSet = CharSet.Auto)]
	private static extern int RmGetList(uint dwSessionHandle, out uint pnProcInfoNeeded, ref uint pnProcInfo,
		[In, Out] RM_PROCESS_INFO[] rgAffectedApps, ref uint lpdwRebootReasons);

	#endregion
}
