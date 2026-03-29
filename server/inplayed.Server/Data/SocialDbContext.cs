using inplayed.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace inplayed.Server.Data;

internal sealed class SocialDbContext(DbContextOptions<SocialDbContext> options) : DbContext(options)
{
	public DbSet<SocialUser> Users => Set<SocialUser>();
	public DbSet<FriendRequest> FriendRequests => Set<FriendRequest>();
	public DbSet<Conversation> Conversations => Set<Conversation>();
	public DbSet<ConversationMember> ConversationMembers => Set<ConversationMember>();
	public DbSet<SocialMessageRecord> Messages => Set<SocialMessageRecord>();
	public DbSet<SocialPost> Posts => Set<SocialPost>();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<SocialUser>()
			.HasIndex(user => user.Username)
			.IsUnique();

		modelBuilder.Entity<FriendRequest>()
			.Property(friendRequest => friendRequest.Status)
			.HasConversion<string>();

		modelBuilder.Entity<Conversation>()
			.Property(conversation => conversation.Kind)
			.HasConversion<string>();

		modelBuilder.Entity<ConversationMember>()
			.HasKey(member => new { member.ConversationId, member.UserId });

		modelBuilder.Entity<ConversationMember>()
			.HasOne(member => member.Conversation)
			.WithMany(conversation => conversation.Members)
			.HasForeignKey(member => member.ConversationId);

		modelBuilder.Entity<ConversationMember>()
			.HasOne(member => member.User)
			.WithMany()
			.HasForeignKey(member => member.UserId);

		modelBuilder.Entity<FriendRequest>()
			.HasOne(friendRequest => friendRequest.Requester)
			.WithMany()
			.HasForeignKey(friendRequest => friendRequest.RequesterId)
			.OnDelete(DeleteBehavior.Restrict);

		modelBuilder.Entity<FriendRequest>()
			.HasOne(friendRequest => friendRequest.Recipient)
			.WithMany()
			.HasForeignKey(friendRequest => friendRequest.RecipientId)
			.OnDelete(DeleteBehavior.Restrict);
	}
}
