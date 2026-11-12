using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.MediaFiles.Commands
{
    public class ImportIncompleteCommand : Command
    {
        public override bool RequiresDiskAccess => true;
        public override bool IsLongRunning => true;
        public override bool IsExclusive => true;
        public override bool SendUpdatesToClient => true;
        public override string CompletionMessage => "Completed";
    }
}
