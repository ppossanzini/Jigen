
// <copyright>
// Changes Copyright Paolo Possanzini
// Licensed under Apache 2.0
// </copyright>


namespace Jigen.Indexer
{
    public interface IProgressReporter
    {
        void Progress(int current, int total);
    }
}
