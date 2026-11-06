using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(222)]
    public class add_episode_viewed : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Alter.Table("Episodes").AddColumn("Viewed").AsBoolean().WithDefaultValue(false);
            Alter.Table("Episodes").AddColumn("ViewedAt").AsDateTime().Nullable();
            Create.Index("IX_Episodes_Viewed").OnTable("Episodes").OnColumn("Viewed").Ascending();
        }
    }
}
