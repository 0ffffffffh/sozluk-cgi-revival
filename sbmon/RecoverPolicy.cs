using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sbmon
{

    public enum RecoverPolicy
    {
        None,
        DoNothing,
        ShutdownMemcachedIfBackendCrashed,
        ShutdownBackendIfMemcachedCrashed,
        RecoverBackendAndPassExistedMemcachedInstance,
        RecoverMemcachedAndPassNewMemcachedInstance,
        RecoverMemcachedOnly,
        ShutdownMemcachedAndRecoverBackendIfBackendCrashed
    }
}
