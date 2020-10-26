#pragma once
#include "Utilities.h"

/* A texture object: NEVER manually delete this - its lifetime is handled by a manager class! */
class Texture {
public:
	~Texture() {
		Memory::SafeRelease(textureView);
		Memory::SafeDelete(textureView);
		Memory::SafeRelease(texture);
		Memory::SafeDelete(texture);
		Memory::SafeDelete(textureBuffer);
	}

	ID3D11ShaderResourceView* GetResourceView() {
		return textureView;
	}

	std::string GetFilepath() {
		return texturePath;
	}

	int GetUsageCount() {
		return useCount;
	}
	void AddUsage() {
		useCount++;
	}
	void RemoveUsage() {
		useCount--;
	}

private:
	friend class TextureManager;

	ID3D11Texture2D* texture = nullptr;
	ID3D11ShaderResourceView* textureView = nullptr;
	DirectX::XMFLOAT2 dimensions;
	char* textureBuffer = nullptr;
	std::string texturePath = "";

	int useCount = 0;
};

/* The texture manager: handles reuse of textures for memory optimisations with bigger scenes */
class TextureManager {
public:
	TextureManager() = default;
	~TextureManager() {
		UnloadAll();
	}

	void Update(float dt);

	Texture* LoadTexture(std::string filepath);
	Texture* LoadTexture(int* colourRGB, int width = 1, int height = 1);

	void UnloadAll() {
		for (int i = 0; i < textures.size(); i++) {
			Memory::SafeDelete(textures[i]);
		}
		textures.clear();
	}

private:
	Texture* CheckPool(std::string filepath);

	std::vector<Texture*> textures = std::vector<Texture*>();

	std::vector<Texture*> texturesSanity = std::vector<Texture*>(); //DO NOT USE - TEMP DEALLOC CHECK
	float deallocCheckAfter = 10.0f;
	float timeSinceDeallocCheck = 0.0f;
};