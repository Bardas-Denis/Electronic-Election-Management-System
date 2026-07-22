using System.Text.Json.Serialization;

namespace Electronic_Election_Management_System.Constants
{
    /// <summary>
    /// Machine-readable error codes returned to the client in the <c>errorCode</c> field.
    /// Each member is serialized as a camelCase string via <see cref="JsonStringEnumMemberName"/>
    /// so that the wire value is stable even if the C# identifier is renamed later.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ErrorCode
    {
        //Authentication Errors

        /// <summary>An account with this email already exists.</summary>
        [JsonStringEnumMemberName("emailAlreadyExists")]
        EmailAlreadyExists,

        /// <summary>Email or password incorrect.</summary>
        [JsonStringEnumMemberName("invalidCredentials")]
        InvalidCredentials,

        //Generic Errors

        /// <summary>The requested resource was not found.</summary>
        [JsonStringEnumMemberName("resourceNotFound")]
        ResourceNotFound,

        //Election Errors

        /// <summary>Invalid type. Accepted values are 'Politic' or 'Comercial'.</summary>
        [JsonStringEnumMemberName("invalidElectionType")]
        InvalidElectionType,

        /// <summary>An election needs to have at least 2 vote options.</summary>
        [JsonStringEnumMemberName("tooFewOptions")]
        TooFewOptions,

        /// <summary>End Date needs to be after start Date.</summary>
        [JsonStringEnumMemberName("invalidDateRange")]
        InvalidDateRange,

        /// <summary>You are not authorized to edit this election.</summary>
        [JsonStringEnumMemberName("notAuthorizedToEdit")]
        NotAuthorizedToEdit,

        /// <summary>You cannot modify the election because it has already been voted on.</summary>
        [JsonStringEnumMemberName("electionHasVotes")]
        ElectionHasVotes,

        /// <summary>You are not authorized to delete this election.</summary>
        [JsonStringEnumMemberName("notAuthorizedToDelete")]
        NotAuthorizedToDelete,

        //Vote Errors

        /// <summary>This election is not currently open for voting.</summary>
        [JsonStringEnumMemberName("electionNotOpen")]
        ElectionNotOpen,

        /// <summary>The selected option does not belong to this election.</summary>
        [JsonStringEnumMemberName("invalidOption")]
        InvalidOption,

        /// <summary>You have already voted in this election.</summary>
        [JsonStringEnumMemberName("alreadyVoted")]
        AlreadyVoted,

        /// <summary>No vote was found for this user in this election.</summary>
        [JsonStringEnumMemberName("voteNotFound")]
        VoteNotFound,

        /// <summary>You can only change your answer once (edit or delete-and-revote).</summary>
        [JsonStringEnumMemberName("voteChangeLimit")]
        VoteChangeLimit,

        /// <summary>Voter declaration is required for this election.</summary>
        [JsonStringEnumMemberName("declarationRequired")]
        DeclarationRequired,

        /// <summary>CNP, full name and residence (county + address) are required for Politic elections.</summary>
        [JsonStringEnumMemberName("incompleteDeclaration")]
        IncompleteDeclaration,

        /// <summary>Invalid CNP.</summary>
        [JsonStringEnumMemberName("invalidCnp")]
        InvalidCnp,

        //User Errors

        /// <summary>Invalid role. Accepted values are 'Admin', 'ElectionManager' or 'Voter'.</summary>
        [JsonStringEnumMemberName("invalidRole")]
        InvalidRole,

        /// <summary>Cannot remove the Admin role from the last remaining admin account.</summary>
        [JsonStringEnumMemberName("lastAdminRoleProtected")]
        LastAdminRoleProtected,

        /// <summary>You cannot delete your own account from the admin panel.</summary>
        [JsonStringEnumMemberName("cannotDeleteSelf")]
        CannotDeleteSelf,

        /// <summary>Cannot delete the last remaining admin account.</summary>
        [JsonStringEnumMemberName("lastAdminDeleteProtected")]
        LastAdminDeleteProtected,

        /// <summary>This user has created at least one election and cannot be deleted. Change their role to Voter instead of deleting them.</summary>
        [JsonStringEnumMemberName("userHasCreatedElections")]
        UserHasCreatedElections,

        /// <summary>This user has cast a vote in a non-anonymous election and cannot be deleted. Change their role to Voter instead of deleting them.</summary>
        [JsonStringEnumMemberName("userHasNonAnonymousVote")]
        UserHasNonAnonymousVote,

        /// <summary>This user has cast a vote (their vote token was consumed) and cannot be deleted. Change their role to Voter instead of deleting them.</summary>
        [JsonStringEnumMemberName("userHasAnonymousVote")]
        UserHasAnonymousVote,

        /// <summary>This user cannot be deleted because other records still reference them.</summary>
        [JsonStringEnumMemberName("userHasDependentRecords")]
        UserHasDependentRecords,
    }
}
