using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.Core.Access.Security
{
    /// <summary>
    /// 瀵嗙爜鏈嶅姟鎺ュ彛
    /// 瀹氫箟瀵嗙爜鐩稿叧鐨勫姞瀵嗐€侀獙璇佸拰寮哄害妫€鏌ュ姛鑳?    /// </summary>
    public interface IPasswordService
    {
        /// <summary>
        /// 瀵瑰瘑鐮佽繘琛屽搱甯屽姞瀵?        /// </summary>
        /// <param name="password">鏄庢枃瀵嗙爜</param>
        /// <returns>鍔犲瘑鍚庣殑瀵嗙爜鍝堝笇鍊硷紙SHA256鍗佸叚杩涘埗瀛楃涓诧級</returns>
        string HashPassword(string password);

        /// <summary>
        /// 楠岃瘉瀵嗙爜鏄惁涓庡搱甯屽€煎尮閰?        /// </summary>
        /// <param name="password">寰呴獙璇佺殑鏄庢枃瀵嗙爜</param>
        /// <param name="hash">宸插瓨鍌ㄧ殑瀵嗙爜鍝堝笇鍊?/param>
        /// <returns>濡傛灉瀵嗙爜鍖归厤杩斿洖true锛屽惁鍒欒繑鍥瀎alse</returns>
        bool VerifyPassword(string password, string hash);

        /// <summary>
        /// 楠岃瘉瀵嗙爜寮哄害鏄惁绗﹀悎瑕佹眰
        /// </summary>
        /// <param name="password">寰呴獙璇佺殑瀵嗙爜</param>
        /// <exception cref="AccessGuardException">褰撳瘑鐮佷笉绗﹀悎寮哄害瑕佹眰鏃舵姏鍑哄紓甯?/exception>
        void ValidatePasswordStrength(string password);
    }
}
