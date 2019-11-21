%module MonoGCDeadlockLib
%{

#include "MonoGCDeadlockLib.h"

%}

//%include <swigignore.i>

//%include "exception.i"

// Specify #define constants, enums, and similar should be simple C# fields instead of bridge calls
%csconst(1);

//%exception {
//    try {
//        $action
//    } catch (const std::exception& e) {
//        SWIG_exception(SWIG_RuntimeError, e.what());
//    }
//}

%include "typemaps.i"

%define %TypeRefParam(TYPE)
    %apply TYPE& INOUT { TYPE& };
%enddef

%TypeRefParam(bool)
%TypeRefParam(int)
%TypeRefParam(double)
%TypeRefParam(long long)
%TypeRefParam(unsigned int)

//handle some nonstandard ref params
%define INOUT_TYPEMAP(TYPE, CTYPE, CSTYPE, TYPECHECKPRECEDENCE)
%typemap(ctype, out="void *") TYPE *INOUT, TYPE &INOUT "CTYPE *"
%typemap(imtype, out="global::System.IntPtr") TYPE *INOUT, TYPE &INOUT "ref CSTYPE"
%typemap(cstype, out="$csclassname") TYPE *INOUT, TYPE &INOUT "ref CSTYPE"
%typemap(csin) TYPE *INOUT, TYPE &INOUT "ref $csinput"

%typemap(in) TYPE *INOUT, TYPE &INOUT
%{ $1 = ($1_ltype)$input; %}

%typecheck(SWIG_TYPECHECK_##TYPECHECKPRECEDENCE) TYPE *INOUT, TYPE &INOUT ""
%enddef

#undef INOUT_TYPEMAP

%include "stdint.i"
%include "stl.i"
%include "std_shared_ptr.i"
//%include "std_string_ref.i"

typedef int ssize_t;
typedef unsigned int size_t;


// callbacks
%define %cs_callback(TYPE, CSTYPE)
    %typemap(ctype) TYPE, TYPE& "void*"
    %typemap(in) TYPE  %{ $1 = (TYPE)$input; %}
    %typemap(in) TYPE& %{ $1 = (TYPE*)&$input; %}
    %typemap(imtype, out="global::System.IntPtr") TYPE, TYPE& "CSTYPE"
    %typemap(cstype, out="global::System.IntPtr") TYPE, TYPE& "CSTYPE"
    %typemap(csin) TYPE, TYPE& "$csinput"
%enddef
%cs_callback(CallbackToCLR_fn,CallbackToCLRDelegate)

// pointer passing magic to cast void pointers as IntPtr and pass the JNIEnv obtained by
// CSharp down to native side ...
//%apply void *VOID_INT_PTR { void * }

%include "MonoGCDeadlockLib.h"

