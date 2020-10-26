#include "TextureManager.h"

/* Deallocate from the texture pool after a time of inactivity */
void TextureManager::Update(float dt)
{
	timeSinceDeallocCheck += dt;
	if (timeSinceDeallocCheck >= deallocCheckAfter) {
		texturesSanity.clear();
		//Debug::Log(" *** TEXTURE USE DEBUG *** ");
		for (int i = 0; i < textures.size(); i++) {
			//Debug::Log("USE: " + std::to_string(textures[i]->useCount) + ", TEXTURE: " + textures[i]->texturePath);
			if (textures[i]->GetUsageCount() <= 0) {
				Memory::SafeDelete(textures[i]);
			}
			else
			{
				texturesSanity.push_back(textures[i]);
			}
		}
		if (texturesSanity.size() != textures.size()) {
			Debug::Log("Successfully deallocated " + std::to_string(textures.size() - texturesSanity.size()) + " texture(s).");
			textures = texturesSanity;
		}
		timeSinceDeallocCheck = 0.0f;
	}
}

/* Load a texture from filepath */
Texture* TextureManager::LoadTexture(std::string filepath)
{
	const char* filename = filepath.c_str();
	Texture* thisTex = CheckPool(filename);
	if (thisTex != nullptr) return thisTex;

	FREE_IMAGE_FORMAT format = FreeImage_GetFileType(filename, 0);
	if (format == FREE_IMAGE_FORMAT::FIF_UNKNOWN) return nullptr;
	FIBITMAP* image = FreeImage_Load(format, filename);

	int BPP = FreeImage_GetBPP(image);
	if (BPP != 32) {
		FIBITMAP* tmp = FreeImage_ConvertTo32Bits(image);
		FreeImage_Unload(image);
		image = tmp;
	}
	BPP = FreeImage_GetBPP(image);
	FreeImage_FlipVertical(image);

	thisTex = new Texture();
	thisTex->texturePath = filepath;
	thisTex->dimensions = XMFLOAT2(FreeImage_GetWidth(image), FreeImage_GetHeight(image));
	int imgLength = thisTex->dimensions.x * thisTex->dimensions.y * 4;
	thisTex->textureBuffer = new char[imgLength];
	memcpy(thisTex->textureBuffer, FreeImage_GetBits(image), imgLength);
	FreeImage_Unload(image);

	D3D11_TEXTURE2D_DESC desc;
	ZeroMemory(&desc, sizeof(D3D11_TEXTURE2D_DESC));
	desc.Width = thisTex->dimensions.x;
	desc.Height = thisTex->dimensions.y;
	desc.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
	desc.Usage = D3D11_USAGE_DYNAMIC;
	desc.CPUAccessFlags = D3D11_CPU_ACCESS_WRITE;
	desc.MiscFlags = 0;
	desc.MipLevels = 1;
	desc.ArraySize = 1;
	desc.SampleDesc.Count = 1;
	desc.SampleDesc.Quality = 0;
	desc.BindFlags = D3D11_BIND_SHADER_RESOURCE;

	D3D11_SUBRESOURCE_DATA initData;
	initData.pSysMem = thisTex->textureBuffer;
	initData.SysMemPitch = thisTex->dimensions.x * 4;
	initData.SysMemSlicePitch = imgLength;
	HR(Shared::m_pDevice->CreateTexture2D(&desc, &initData, &thisTex->texture));

	D3D11_SHADER_RESOURCE_VIEW_DESC srvDesc;
	srvDesc.Format = desc.Format;
	srvDesc.ViewDimension = D3D11_SRV_DIMENSION_TEXTURE2D;
	srvDesc.Texture2D.MipLevels = desc.MipLevels;
	srvDesc.Texture2D.MostDetailedMip = desc.MipLevels - 1;
	HR(Shared::m_pDevice->CreateShaderResourceView(thisTex->texture, &srvDesc, &thisTex->textureView));

	thisTex->AddUsage();
	textures.push_back(thisTex);
	return thisTex;
}

/* Load a colour as a texture resource */
Texture* TextureManager::LoadTexture(int* colourRGB, int width, int height)
{
	std::string madeupFilepath = "_COL_" + std::to_string(R) + std::to_string(G) + std::to_string(B) + "_DIMS_" + std::to_string(width) + std::to_string(height);
	Texture* thisTex = CheckPool(madeupFilepath);
	if (thisTex != nullptr) return thisTex;

	thisTex = new Texture();
	thisTex->texturePath = madeupFilepath;
	thisTex->dimensions = XMFLOAT2(width, height);
	int imgLength = thisTex->dimensions.x * thisTex->dimensions.y * 4;
	thisTex->textureBuffer = new char[imgLength];
	char R = colourRGB[0]; char G = colourRGB[1]; char B = colourRGB[2]; char A = 255; 
	for (int i = 0; i < width * height; i += 4) {
		thisTex->textureBuffer[i] = B;
		thisTex->textureBuffer[i + 1] = G;
		thisTex->textureBuffer[i + 2] = R;
		thisTex->textureBuffer[i + 3] = A;
	}

	D3D11_TEXTURE2D_DESC desc;
	ZeroMemory(&desc, sizeof(D3D11_TEXTURE2D_DESC));
	desc.Width = thisTex->dimensions.x;
	desc.Height = thisTex->dimensions.y;
	desc.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
	desc.Usage = D3D11_USAGE_DYNAMIC;
	desc.CPUAccessFlags = D3D11_CPU_ACCESS_WRITE;
	desc.MiscFlags = 0;
	desc.MipLevels = 1;
	desc.ArraySize = 1;
	desc.SampleDesc.Count = 1;
	desc.SampleDesc.Quality = 0;
	desc.BindFlags = D3D11_BIND_SHADER_RESOURCE;

	D3D11_SUBRESOURCE_DATA initData;
	initData.pSysMem = thisTex->textureBuffer;
	initData.SysMemPitch = thisTex->dimensions.x * 4;
	initData.SysMemSlicePitch = imgLength;
	HR(Shared::m_pDevice->CreateTexture2D(&desc, &initData, &thisTex->texture));

	D3D11_SHADER_RESOURCE_VIEW_DESC srvDesc;
	srvDesc.Format = desc.Format;
	srvDesc.ViewDimension = D3D11_SRV_DIMENSION_TEXTURE2D;
	srvDesc.Texture2D.MipLevels = desc.MipLevels;
	srvDesc.Texture2D.MostDetailedMip = desc.MipLevels - 1;
	HR(Shared::m_pDevice->CreateShaderResourceView(thisTex->texture, &srvDesc, &thisTex->textureView));

	thisTex->AddUsage();
	textures.push_back(thisTex);
	return thisTex;
}

/* Gets a texture reference from the pool if it already exists */
Texture* TextureManager::CheckPool(std::string filepath)
{
	for (int i = 0; i < textures.size(); i++) {
		if (textures[i]->texturePath == filepath) {
			textures[i]->AddUsage();
			return textures[i];
		}
	}
	return nullptr;
}
