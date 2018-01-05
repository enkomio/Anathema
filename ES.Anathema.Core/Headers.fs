namespace ES.Anathema.Core
#nowarn "9"

open System
open System.Runtime.InteropServices

[<AutoOpen>]
module Headers =
    type Protection =        
        | PAGE_NOACCESS = 0x01
        | PAGE_READONLY = 0x02
        | PAGE_READWRITE = 0x04
        | PAGE_WRITECOPY = 0x08
        | PAGE_EXECUTE = 0x10
        | PAGE_EXECUTE_READ = 0x20
        | PAGE_EXECUTE_READWRITE = 0x40
        | PAGE_EXECUTE_WRITECOPY = 0x80
        | PAGE_GUARD = 0x100
        | PAGE_NOCACHE = 0x200
        | PAGE_WRITECOMBINE = 0x400

    type CorJitFlag =
        | CORJIT_FLG_SPEED_OPT = 0x00000001
        | CORJIT_FLG_SIZE_OPT = 0x00000002
        | CORJIT_FLG_DEBUG_CODE = 0x00000004 // generate "debuggable" code (no code-mangling optimizations)
        | CORJIT_FLG_DEBUG_EnC = 0x00000008 // We are in Edit-n-Continue mode
        | CORJIT_FLG_DEBUG_INFO = 0x00000010 // generate line and local-var info
        | CORJIT_FLG_LOOSE_EXCEPT_ORDER = 0x00000020 // loose exception order
        | CORJIT_FLG_TARGET_PENTIUM = 0x00000100
        | CORJIT_FLG_TARGET_PPRO = 0x00000200
        | CORJIT_FLG_TARGET_P4 = 0x00000400
        | CORJIT_FLG_TARGET_BANIAS = 0x00000800
        | CORJIT_FLG_USE_FCOMI = 0x00001000 // Generated code may use fcomi(p) instruction
        | CORJIT_FLG_USE_CMOV = 0x00002000 // Generated code may use cmov instruction
        | CORJIT_FLG_USE_SSE2 = 0x00004000 // Generated code may use SSE-2 instructions
        | CORJIT_FLG_PROF_CALLRET = 0x00010000 // Wrap method calls with probes
        | CORJIT_FLG_PROF_ENTERLEAVE = 0x00020000 // Instrument prologues/epilogues
        | CORJIT_FLG_PROF_INPROC_ACTIVE_DEPRECATED = 0x00040000
        // Inprocess debugging active requires different instrumentation
        | CORJIT_FLG_PROF_NO_PINVOKE_INLINE = 0x00080000 // Disables PInvoke inlining
        | CORJIT_FLG_SKIP_VERIFICATION = 0x00100000
        // (lazy) skip verification - determined without doing a full resolve. See comment below
        | CORJIT_FLG_PREJIT = 0x00200000 // jit or prejit is the execution engine.
        | CORJIT_FLG_RELOC = 0x00400000 // Generate relocatable code
        | CORJIT_FLG_IMPORT_ONLY = 0x00800000 // Only import the function
        | CORJIT_FLG_IL_STUB = 0x01000000 // method is an IL stub
        | CORJIT_FLG_PROCSPLIT = 0x02000000 // JIT should separate code into hot and cold sections
        | CORJIT_FLG_BBINSTR = 0x04000000 // Collect basic block profile information
        | CORJIT_FLG_BBOPT = 0x08000000 // Optimize method based on profile information
        | CORJIT_FLG_FRAMED = 0x10000000 // All methods have an EBP frame
        | CORJIT_FLG_ALIGN_LOOPS = 0x20000000 // add NOPs before loops to align them at 16 byte boundaries
        | CORJIT_FLG_PUBLISH_SECRET_PARAM = 0x40000000
        // JIT must place stub secret param into local 0.  (used by IL stubs)

    type CorInfoCallConv =
        | C = 1
        | DEFAULT = 0
        | EXPLICITTHIS = 64
        | FASTCALL = 4
        | FIELD = 6
        | GENERIC = 16
        | HASTHIS = 32
        | LOCAL_SIG = 7
        | MASK = 15
        | NATIVEVARARG = 11
        | PARAMTYPE = 128
        | PROPERTY = 8
        | STDCALL = 2
        | THISCALL = 3
        | VARARG = 5

    type CorInfoType =
        | BOOL = 2uy
        | BYREF = 18uy
        | BYTE = 4uy
        | CHAR = 3uy
        | CLASS = 20uy
        | COUNT = 23uy
        | DOUBLE = 15uy
        | FLOAT = 14uy
        | INT = 8uy
        | LONG = 10uy
        | NATIVEINT = 12uy
        | NATIVEUINT = 13uy
        | PTR = 17uy
        | REFANY = 21uy
        | SHORT = 6uy
        | STRING = 16uy
        | UBYTE = 5uy
        | UINT = 9uy
        | ULONG = 11uy
        | UNDEF = 0uy
        | USHORT = 7uy
        | VALUECLASS = 19uy
        | VAR = 22uy
        | VOID = 1uy

    [<Struct>]
    [<StructLayout(LayoutKind.Sequential)>]
    type CorinfoSigInst = 
        val mutable ClassInstCount: UInt32
        val mutable ClassInst: nativeptr<IntPtr>
        val mutable MethInstCount: UInt32
        val mutable MethInst: nativeptr<IntPtr>
        
    [<Struct>]
    [<StructLayout(LayoutKind.Sequential)>]
    type CorinfoSigInfo = 
        val mutable CallConv: CorInfoCallConv
        val mutable RetTypeClass: IntPtr
        val mutable RetTypeSigClass: IntPtr
        val mutable RetType: CorInfoType
        val mutable Flags: Byte
        val mutable NumArgs: UInt16
        val mutable SigInst: CorinfoSigInst
        val mutable Args: IntPtr
        val mutable Token: UInt32
        val mutable Sig: IntPtr
        val mutable Scope: IntPtr
        
    [<Struct>]
    [<StructLayout(LayoutKind.Sequential, Pack = 1)>]
    type CorMethodInfo = 
        val mutable MethodHandle: IntPtr
        val mutable ModuleHandle: IntPtr
        val mutable IlCode: IntPtr
        val mutable IlCodeSize: UInt32
        val mutable MaxStack: UInt16
        val mutable EHCount: UInt16
        val mutable CorInfoOptions: UInt32
        val mutable Args: CorinfoSigInfo
        val mutable Locals: CorinfoSigInfo

    [<Struct>]
    [<StructLayout(LayoutKind.Sequential, Pack = 1)>]
    type CorILMethodSectEhSmall =
        val mutable Flags: UInt16
        val mutable TryOffset: UInt16
        val mutable TryLength: Byte
        val mutable HandlerOffset: UInt16
        val mutable HandlerLength: Byte
        val mutable ClassToken: UInt32

    [<Struct>]
    [<StructLayout(LayoutKind.Sequential, Pack = 1)>]
    type CorILMethodSectEhFat =
        val mutable Flags: UInt32
        val mutable TryOffset: UInt32
        val mutable TryLength: UInt32
        val mutable HandlerOffset: UInt32
        val mutable HandlerLength: UInt32
        val mutable ClassToken: UInt32
    