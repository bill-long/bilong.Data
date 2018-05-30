using System;

namespace bilong.Data.Repository
{
    public interface IStorable
    {
        string Id { get; set; }
        DateTime CreatedTime { get; set; }
        string CreatorId { get; set; }
        DateTime ModifiedTime { get; set; }
        string ModifierId { get; set; }
    }
}
