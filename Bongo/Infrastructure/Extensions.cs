using Bongo.Models;

namespace Bongo.Infrastructure
{
    public static class Extensions
    {
        public static BongoUser EncryptUser(this BongoUser user)
        {
            user.Email = Encryption.Encrypt(user.Email);
            user.MergeKey = Encryption.Encrypt(user.MergeKey);
            user.SecurityQuestion= Encryption.Encrypt(user.SecurityQuestion);
            user.SecurityAnswer= Encryption.Encrypt(user.SecurityAnswer);

            return user;
        }
        public static BongoUser DecryptUser(this BongoUser user)
        {
            user.Email = Encryption.Decrypt(user.Email);
            user.MergeKey = Encryption.Decrypt(user.MergeKey);
            user.SecurityQuestion = Encryption.Decrypt(user.SecurityQuestion);
            user.SecurityAnswer = Encryption.Decrypt(user.SecurityAnswer);

            return user;
        }
    }
}
