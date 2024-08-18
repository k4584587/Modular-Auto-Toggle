using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using ToggleTool.Global;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ToggleTool.Utils
{
    public class ImageLoader : MonoBehaviour
    {
        public static readonly Dictionary<string, ImageLoader> instance = new Dictionary<string, ImageLoader>()
        {
            { "ToggleON", new ImageLoader(FilePaths.IMAGE_NAME_TOGGLE_ON) },
            { "ToggleOFF", new ImageLoader(FilePaths.IMAGE_NAME_TOGGLE_OFF) }
        };

        private Texture2D _iconTexture;  // 로드된 텍스처를 저장할 필드

        public string Filename { get; private set; }

        public Texture2D iconTexture
        {
            get
            {
                if (_iconTexture == null)
                {
                    Debug.LogWarning($"iconTexture is null, attempting to reload: {Filename}");
                    LoadImage(Filename);
                }
                return _iconTexture;
            }
            private set
            {
                _iconTexture = value;
            }
        }

        public ImageLoader(string filename)
        {
            Filename = filename;
            LoadImage(filename);
        }

        public void LoadImage(string filename)
        {
            try
            {
                // 디렉토리 확인 및 생성
                if (!Directory.Exists(FilePaths.TARGET_RESOURCES_PATH))
                {
                    Directory.CreateDirectory(FilePaths.TARGET_RESOURCES_PATH);
                    Debug.Log($"Directory created: {FilePaths.TARGET_RESOURCES_PATH}");
                }

                // 파일 존재 여부 확인 및 복사
                string targetFilePath = FilePaths.TARGET_RESOURCES_PATH + filename;
                string sourceFilePath = FilePaths.PACKAGE_RESOURCES_PATH + filename;
                
                if (!File.Exists(targetFilePath))
                {
                    if (File.Exists(sourceFilePath))
                    {
                        // 이미지 파일 복사
                        File.Copy(sourceFilePath, targetFilePath);

                        // .meta 파일 복사
                        string sourceMetaFilePath = sourceFilePath + ".meta";
                        string targetMetaFilePath = targetFilePath + ".meta";

                        if (File.Exists(sourceMetaFilePath))
                        {
                            File.Copy(sourceMetaFilePath, targetMetaFilePath);
                            Debug.Log($"Meta file copied from {sourceMetaFilePath} to {targetMetaFilePath}");
                        }
                        else
                        {
                            Debug.LogWarning($".meta file does not exist: {sourceMetaFilePath}");
                        }

#if UNITY_EDITOR
                        AssetDatabase.Refresh();
#endif
                        Debug.Log($"File copied from {sourceFilePath} to {targetFilePath}");
                    }
                    else
                    {
                        Debug.LogError($"Source file does not exist: {sourceFilePath}");
                        return; // 파일이 존재하지 않으면 메서드 종료
                    }
                }

                // 기존 텍스처가 있으면 해제
                if (iconTexture != null)
                {
                    UnityEngine.Object.DestroyImmediate(iconTexture);
                    iconTexture = null;
                }

                // 텍스처 로드
                if (File.Exists(targetFilePath))
                {
                    byte[] fileData = File.ReadAllBytes(targetFilePath);
                    iconTexture = new Texture2D(2, 2);
                    if (iconTexture.LoadImage(fileData))
                    {
                        Debug.Log($"Texture loaded successfully: {targetFilePath}");
                    }
                    else
                    {
                        Debug.LogError($"Failed to load texture from data: {targetFilePath}");
                        iconTexture = null;
                    }
                }
                else
                {
                    Debug.LogError($"File not found after attempted copy: {targetFilePath}");
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                Debug.LogError($"Unauthorized access exception: {ex.Message}");
            }
            catch (IOException ex)
            {
                Debug.LogError($"IO exception occurred: {ex.Message}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"An unexpected error occurred: {ex.Message}");
            }
        }
    }
}
