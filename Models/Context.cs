using Domain;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace API.Models
{
    public class Context : IdentityDbContext<AppUser>
    {
        public Context(DbContextOptions<Context> options) : base(options)
        {
        }

        public virtual DbSet<Message> Messages { get; set; }
        public virtual DbSet<Recipient> Recipients { get; set; }
        public virtual DbSet<FriendRequest> FriendRequests { get; set; }
        public virtual DbSet<Friend> Friends { get; set; }
        public virtual DbSet<Conversation> Conversations { get; set; }
        public virtual DbSet<File> Files { get; set; }
        public virtual DbSet<Image> Images { get; set; }
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.Entity<Conversation>().HasMany(t => t.Messages).WithOne(t => t.Conversation);
            builder.Entity<Conversation>().HasMany(t => t.Recipients).WithOne(t => t.Conversation);
            builder.Entity<Conversation>().HasMany(t => t.Files).WithOne(t => t.Conversation);
            builder.Entity<Conversation>().Property(i => i.Id).ValueGeneratedNever();
            builder.Entity<Friend>().Property(i => i.Id).ValueGeneratedNever();
            builder.Entity<Image>().Property(i => i.Id).ValueGeneratedNever();
            builder.Entity<File>().Property(i => i.Id).ValueGeneratedNever();

        }

    }
}