using System;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Radiant
{
    public interface IGPU
    {
        Func<T[,], T[,], Task<T[,]>> CreateKernel2DExpr<T>(Expression<Func<GPUContext, T[,], T[,], T>> expression, KernelOptions options) where T : unmanaged;
    }
}
