﻿namespace Cake.AzureDevOps.Tests.PullRequest.CommentThread
{
    using Cake.AzureDevOps.PullRequest.CommentThread;
    using Microsoft.TeamFoundation.SourceControl.WebApi;
    using Shouldly;
    using Xunit;

    public sealed class AzureDevOpsCommentTests
    {
        public sealed class TheCtor
        {
            [Fact]
            public void Should_Throw_If_Comment_Is_Null()
            {
                // Given, When
                var result = Record.Exception(() => new AzureDevOpsComment(null));

                // Then
                result.IsArgumentNullException("comment");
            }

            [Fact]
            public void Should_Throw_If_Comment_Content_Is_Null()
            {
                // Given, When
                var result = Record.Exception(() => new AzureDevOpsComment(null, false));

                // Then
                result.IsArgumentNullException("content");
            }

            [Fact]
            public void Should_Throw_If_Comment_Content_Is_Empty()
            {
                // Given, When
                var result = Record.Exception(() => new AzureDevOpsComment(string.Empty, false));

                // Then
                result.IsArgumentOutOfRangeException("content");
            }

            [Fact]
            public void Should_Return_Empty_Comment()
            {
                // Given, When
                var comment = new AzureDevOpsComment();

                // Then
                comment.ShouldNotBeNull();
                comment.Content.ShouldBe(default(string));
                comment.IsDeleted.ShouldBe(default(bool));
                comment.CommentType.ShouldBe(default(AzureDevOpsCommentType));
            }

            [Fact]
            public void Should_Return_Valid_Comment_With_Default_Comment_Type()
            {
                // Given, When
                var comment = new AzureDevOpsComment("Hello", false);

                // Then
                comment.ShouldNotBeNull();
                comment.Content.ShouldBe("Hello");
                comment.IsDeleted.ShouldBeFalse();
                comment.CommentType.ShouldBe(default(AzureDevOpsCommentType));
            }

            [Fact]
            public void Should_Return_Valid_Comment_Via_Initializers()
            {
                // Given, When
                var comment = new AzureDevOpsComment { Content = "All good", IsDeleted = false, CommentType = AzureDevOpsCommentType.CodeChange };

                // Then
                comment.ShouldNotBeNull();
                comment.Content.ShouldBe("All good");
                comment.IsDeleted.ShouldBeFalse();
                comment.CommentType.ShouldBe(AzureDevOpsCommentType.CodeChange);
            }
        }
    }
}
