using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Mu3Library.Editor.FileUtil {
    public static class FileCreator {



        #region Utility
        /// <summary>
        /// ScriptableObject 에셋을 만들어 저장한다. 폴더가 없으면 만든다.
        /// <br/> 폴더는 <c>Assets</c>이거나 그 아래여야 한다. <c>Packages/...</c>처럼 <c>Assets</c> 밖의 경로는
        /// 임베디드·로컬 패키지처럼 쓸 수 있는 패키지의 경로라도 받지 않는다. 그런 경로를 받으면 받을 수 없는 경로를 적은
        /// 오류를 한 번 남기고 null을 돌려주며, 예외를 던지지 않는다.
        /// <br/> <c>{directory}/{fileName}.asset</c>에 에셋이 이미 있으면 거절하거나 오류를 남기지 않고 그 에셋을 새 에셋으로 바꾼다.
        /// 기존 에셋 객체는 파괴되고 그 설정은 사라지므로, 그 경로에 지켜야 할 에셋이 있을 수 있으면 부르기 전에
        /// <c>AssetDatabase.LoadAssetAtPath</c>로 확인한다.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="directory"> Assets 폴더를 기준으로 한 상대 경로 (<c>Assets</c> 또는 <c>Assets/...</c>) </param>
        /// <param name="fileName"> 확장자를 제외한 파일 이름 </param>
        /// <returns> 만든 에셋, 인자가 비었거나 폴더가 <c>Assets</c> 밖이면 null </returns>
        public static T CreateScriptableObject<T>(string directory, string fileName) where T : ScriptableObject {
            if(string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName)) {
                Debug.LogError($"Property not enough. directory: {directory}, fileName: {fileName}");

                return null;
            }

            if(!FilePathConvertor.IsAssetPath(directory.Replace('\\', '/'))) {
                Debug.LogError($"Cannot create '{fileName}.asset' in '{directory}'. The folder must be 'Assets' or under it; package folders are not supported.");

                return null;
            }

            string systemDirectory = FilePathConvertor.AssetPathToSystemPath(directory);
            DirectoryInfo di = new DirectoryInfo(systemDirectory);
            // 만약 폴더가 존재하지 않으면 생성한다.
            if(!di.Exists) {
                di.Create();
            }

            string relativeFilePath = $"{directory}/{fileName}.asset";

            T instance = ScriptableObject.CreateInstance<T>();

            // ScriptableObject를 에셋으로 저장
            AssetDatabase.CreateAsset(instance, relativeFilePath);
            AssetDatabase.SaveAssets();

            //Debug.Log($"ScriptableObject Created. path: {relativeFilePath}");

            return instance;
        }

        /// <summary>
        /// 생성한 스크립트를 UTF-8 BOM으로 저장한다.
        /// </summary>
        /// <param name="systemDirectory"> 저장 폴더의 시스템 경로 </param>
        /// <param name="fileName"> 확장자를 제외한 파일 이름 </param>
        /// <param name="body"> 스크립트 본문 </param>
        /// <returns> 저장한 파일의 시스템 경로 </returns>
        public static string WriteScript(string systemDirectory, string fileName, string body) {
            if(string.IsNullOrEmpty(systemDirectory) || string.IsNullOrEmpty(fileName)) {
                Debug.LogError($"Property not enough. directory: {systemDirectory}, fileName: {fileName}");

                return "";
            }

            string filePath = Path.Combine(systemDirectory, $"{fileName}.cs");
            File.WriteAllText(filePath, body, new UTF8Encoding(true));

            return filePath;
        }

        /// <summary>
        /// 생성한 스크립트를 define 심볼 조건 안에 넣는다.
        /// <br/> 선택 패키지를 쓰는 스크립트가 그 패키지가 없는 프로젝트에서 컴파일되지 않게 할 때 쓴다.
        /// <br/> 맨 앞의 주석 줄(자동 생성 표시)은 조건 밖에 남긴다. 심볼이 비어 있으면 스크립트를 그대로 돌려준다.
        /// </summary>
        /// <param name="script"> 스크립트 본문 </param>
        /// <param name="defineSymbol"> 조건으로 쓸 define 심볼, 앞뒤 공백은 무시한다 </param>
        internal static string WrapInDefineSymbol(string script, string defineSymbol) {
            if(script == null) {
                script = "";
            }

            if(string.IsNullOrWhiteSpace(defineSymbol)) {
                return script;
            }

            int bodyStart = 0;
            while(bodyStart < script.Length && string.CompareOrdinal(script, bodyStart, "//", 0, 2) == 0) {
                int lineEnd = script.IndexOf('\n', bodyStart);
                bodyStart = lineEnd < 0 ? script.Length : lineEnd + 1;
            }

            string newLine = System.Environment.NewLine;
            string bodyEnd = script.Length == 0 || script.EndsWith("\n") ? "" : newLine;

            return script.Substring(0, bodyStart)
                + $"#if {defineSymbol.Trim()}{newLine}"
                + script.Substring(bodyStart)
                + bodyEnd
                + $"#endif{newLine}";
        }
        #endregion
    }
}