using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

// An anonymous, non-inherited handle owned by the game. Windows closes it
// even if the game crashes or is ended, terminating only its assigned helper.
public sealed class GBWindowsChildJob:IDisposable {
    sealed class JobHandle:SafeHandleZeroOrMinusOneIsInvalid {
        public JobHandle(IntPtr value):base(true){SetHandle(value);}
        protected override bool ReleaseHandle(){return CloseHandle(handle);}
    }
    [StructLayout(LayoutKind.Sequential)] struct BasicLimits {
        public long processTime,jobTime;public uint flags;public UIntPtr minimumWorkingSet,maximumWorkingSet;
        public uint activeProcesses;public UIntPtr affinity;public uint priority,scheduling;
    }
    [StructLayout(LayoutKind.Sequential)] struct IoCounters {
        public ulong readOperations,writeOperations,otherOperations,readBytes,writeBytes,otherBytes;
    }
    [StructLayout(LayoutKind.Sequential)] struct ExtendedLimits {
        public BasicLimits basic;public IoCounters io;public UIntPtr processMemory,jobMemory,peakProcessMemory,peakJobMemory;
    }
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr CreateJobObject(IntPtr attributes,string name);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool SetInformationJobObject(JobHandle job,int type,ref ExtendedLimits limits,uint length);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool AssignProcessToJobObject(JobHandle job,IntPtr process);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool CloseHandle(IntPtr handle);
    JobHandle handle;
    public bool Attached {get;private set;}
    public static GBWindowsChildJob Create(){
        if(Environment.OSVersion.Platform!=PlatformID.Win32NT)return null;
        var value=CreateJobObject(IntPtr.Zero,null);if(value==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error(),"Could not own the private-room helper");
        var job=new GBWindowsChildJob{handle=new JobHandle(value)};
        var limits=new ExtendedLimits();limits.basic.flags=0x2000; // JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
        if(!SetInformationJobObject(job.handle,9,ref limits,(uint)Marshal.SizeOf(typeof(ExtendedLimits)))){
            int error=Marshal.GetLastWin32Error();job.Dispose();throw new Win32Exception(error,"Could not configure private-room cleanup");
        }
        return job;
    }
    public void Assign(Process child){
        if(handle==null||handle.IsClosed)throw new ObjectDisposedException("Private-room job");
        if(!AssignProcessToJobObject(handle,child.Handle))throw new Win32Exception(Marshal.GetLastWin32Error(),"Could not attach private-room cleanup");
        Attached=true;
    }
    public void Dispose(){handle?.Dispose();handle=null;}
}
