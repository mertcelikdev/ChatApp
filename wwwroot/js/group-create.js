// Group Creation Logic
(function() {
    // ==== STATE ====
    const currentUser = window.__currentUser || '';
    const currentUserDisplayName = window.__currentUserDisplayName || '';
    const currentUserProfileImage = window.__currentUserProfileImage || '';
    
    let selectedUsers = new Set();
    let groupImageFile = null;
    let userSearchTimeout = null;

    // ==== SAFE HELPERS (camelCase / PascalCase uyumsuzluğu için) ====
    const getId = (u) => u?.Id ?? u?.id;
    const getUsername = (u) => u?.Username || u?.username || '';
    const getDisplayName = (u) => u?.DisplayName || u?.displayName || '';
    const getProfileImage = (u) => u?.ProfileImageUrl || u?.profileImageUrl || '';
    const getLabel = (u) => (getDisplayName(u) || getUsername(u) || '').trim();
    const escapeAttr = (s) => (s || '').replace(/'/g, "&#39;");

    // ==== DOM ELEMENTS ====
    const createGroupForm = document.getElementById('createGroupForm');
    const groupImageUpload = document.getElementById('groupImageUpload');
    const groupImageInput = document.getElementById('groupImageInput');
    const groupImagePreview = document.getElementById('groupImagePreview');
    const uploadPlaceholder = document.getElementById('uploadPlaceholder');
    const groupName = document.getElementById('groupName');
    const groupDescription = document.getElementById('groupDescription');
    const userSearch = document.getElementById('userSearch');
    const userSelection = document.getElementById('userSelection');
    const selectedUsersDiv = document.getElementById('selectedUsers');
    const userSelectionEmpty = document.getElementById('userSelectionEmpty');
    const createGroupBtn = document.getElementById('createGroupBtn');
    const groupPrivate = document.getElementById('groupPrivate');

    if (!createGroupForm) {
        console.error('Group creation form not found');
        return;
    }

    // ==== IMAGE UPLOAD ====
    groupImageUpload?.addEventListener('click', () => {
        groupImageInput.click();
    });

    groupImageInput?.addEventListener('change', (e) => {
        const file = e.target.files[0];
        if (!file) return;

        // Validate file type
        if (!file.type.startsWith('image/')) {
            alert('Lütfen sadece resim dosyaları seçin.');
            return;
        }

        // Validate file size (5MB max)
        if (file.size > 5 * 1024 * 1024) {
            alert('Dosya boyutu çok büyük. Maksimum 5MB olabilir.');
            return;
        }

        groupImageFile = file;

        // Show preview
        const reader = new FileReader();
        reader.onload = (e) => {
            groupImagePreview.src = e.target.result;
            groupImagePreview.classList.remove('d-none');
            uploadPlaceholder.style.display = 'none';
            groupImageUpload.classList.add('has-image');
        };
        reader.readAsDataURL(file);
    });

    // Remove image
    groupImagePreview?.addEventListener('click', (e) => {
        e.stopPropagation();
        if (confirm('Grup fotoğrafını kaldırmak istediğinizden emin misiniz?')) {
            groupImageFile = null;
            groupImagePreview.classList.add('d-none');
            uploadPlaceholder.style.display = 'block';
            groupImageUpload.classList.remove('has-image');
            groupImageInput.value = '';
        }
    });

    // ==== USER SEARCH ====
    userSearch?.addEventListener('input', (e) => {
        const searchTerm = e.target.value.trim();
        
        // Clear previous timeout
        if (userSearchTimeout) {
            clearTimeout(userSearchTimeout);
        }

        if (searchTerm.length === 0) {
            userSelection.innerHTML = `
                <div class="empty-state">
                    <i class="bi bi-search"></i>
                    <p>Kullanıcı aramaya başlayın</p>
                    <small>İsim veya kullanıcı adı ile arayabilirsiniz</small>
                </div>
            `;
            return;
        }

        if (searchTerm.length < 2) {
            userSelection.innerHTML = `
                <div class="empty-state">
                    <i class="bi bi-keyboard"></i>
                    <p>En az 2 karakter girin</p>
                </div>
            `;
            return;
        }

        // Debounce search
        userSearchTimeout = setTimeout(() => {
            searchUsers(searchTerm);
        }, 300);
    });

    function searchUsers(searchTerm) {
        userSelection.innerHTML = `
            <div class="empty-state">
                <div class="spinner-border" role="status" style="width:2rem;height:2rem;"></div>
                <p>Aranıyor...</p>
            </div>
        `;

        const searchUrl = `/Chat/SearchUsers?query=${encodeURIComponent(searchTerm)}`;

        fetch(searchUrl)
            .then(response => {
                if (!response.ok) {
                    throw new Error(`HTTP error! status: ${response.status}`);
                }
                return response.json();
            })
            .then(data => {
                if (data.success && data.users) {
                    renderUserList(data.users);
                } else {
                    userSelection.innerHTML = `
                        <div class="empty-state">
                            <i class="bi bi-person-x"></i>
                            <p>Kullanıcı bulunamadı</p>
                            <small>${data.message || 'Farklı anahtar kelimeler deneyin'}</small>
                        </div>
                    `;
                }
            })
            .catch(error => {
                console.error('User search error:', error);
                userSelection.innerHTML = `
                    <div class="empty-state">
                        <i class="bi bi-exclamation-triangle" style="color: var(--color-danger);"></i>
                        <p>Arama hatası</p>
                        <small>${error.message}</small>
                    </div>
                `;
            });
    }

    function renderUserList(users) {
        if (!Array.isArray(users) || users.length === 0) {
            userSelection.innerHTML = '<div class="empty-state"><i class="bi bi-person-x"></i><p>Kullanıcı bulunamadı</p></div>';
            return;
        }

        // Filtre: mevcut kullanıcı + zaten seçili olanlar
        const filteredUsers = users.filter(u => {
            const uname = getUsername(u);
            const id = getId(u);
            return uname && uname !== currentUser && !selectedUsers.has(id);
        });

        if (filteredUsers.length === 0) {
            userSelection.innerHTML = '<div class="empty-state"><i class="bi bi-check-circle"></i><p>Tüm sonuçlar zaten seçili</p></div>';
            return;
        }

        userSelection.innerHTML = filteredUsers.map(u => {
            const id = getId(u);
            const uname = getUsername(u);
            const dname = getDisplayName(u) || uname;
            const img = getProfileImage(u);
            const label = getLabel(u);
            const initial = label ? label.charAt(0).toUpperCase() : '?';
            return `
                <div class="user-item" data-user-id="${id}" onclick="selectUser(${id}, '${escapeAttr(uname)}', '${escapeAttr(dname)}', '${escapeAttr(img)}')">
                    <div class="user-avatar">
                        ${img ? `<img src="${img}" alt="${escapeAttr(dname)}" style="width:100%;height:100%;object-fit:cover;border-radius:50%;">` : initial}
                    </div>
                    <div class="user-info">
                        <h6>${dname}</h6>
                        <small>@${uname}</small>
                    </div>
                </div>`;
        }).join('');
    }

    // ==== LIMITLER ====
    const MIN_MEMBERS = 1;
    const MAX_MEMBERS = 256;

    // ==== USER SELECTION (YENİ) ====
    let selectedUserData = new Map();

    window.selectUser = function(userId, username, displayName, profileImageUrl) {
        if (selectedUsers.has(userId)) return;
        if (selectedUsers.size >= MAX_MEMBERS) {
            showTempMessage('Üye limiti (' + MAX_MEMBERS + ') doldu', 'warning');
            return;
        }

        selectedUserData.set(userId, {
            id: userId,
            username,
            displayName: displayName || username,
            profileImageUrl
        });

        selectedUsers.add(userId);
        renderSelectedUsers();

        const userItem = document.querySelector(`[data-user-id="${userId}"]`);
        userItem?.remove();

        const remainingItems = userSelection.querySelectorAll('.user-item');
        if (remainingItems.length === 0) {
            userSelection.innerHTML = '<div class="empty-state"><i class="bi bi-search"></i><p>Daha fazla kullanıcı aramak için devam edin</p></div>';
        }
    };

    function renderSelectedUsers() {
        const count = selectedUsers.size;
        const badge = document.getElementById('memberCountBadge');
        const limitWarn = document.getElementById('limitWarning');
        if (badge) badge.textContent = `${count} / ${MAX_MEMBERS}`;
        if (limitWarn) {
            if (count >= MAX_MEMBERS) limitWarn.classList.remove('d-none'); else limitWarn.classList.add('d-none');
        }
        if (count === 0) {
            selectedUsersDiv.innerHTML = `
                <div class="empty-state">
                    <i class="bi bi-person-plus"></i>
                    <p>Seçilen üyeler burada görünecek</p>
                    <small>En az ${MIN_MEMBERS} üye seçmelisiniz</small>
                </div>`;
            return;
        }

        selectedUsersDiv.innerHTML = `
            <div class="mb-2 small" style="color: var(--color-text-dim);">
                Üye sayısı: ${count} / ${MAX_MEMBERS}
            </div>
            <div class="selected-user-chip-container">${Array.from(selectedUsers).map(uid => {
                const u = selectedUserData.get(uid);
                if (!u) return '';
                const label = (u.displayName || u.username).trim();
                const initial = label.charAt(0).toUpperCase();
                return `
                    <div class="selected-user-chip" data-user-id="${uid}" title="@${u.username}">
                        <span class="chip-initial">${initial}</span>
                        <span class="chip-label">${label}</span>
                        <button type="button" class="remove-user" onclick="removeUser(${uid})" aria-label="Kaldır">
                            <i class="bi bi-x"></i>
                        </button>
                    </div>`;
            }).join('')}</div>`;
    }

    function getSelectedUserData(userId) { return selectedUserData.get(userId); }

    window.removeUser = function(userId) {
        if (!selectedUsers.has(userId)) return;
        selectedUsers.delete(userId);
        selectedUserData.delete(userId);
        renderSelectedUsers();
        if (userSearch && userSearch.value.trim().length >= 2) {
            searchUsers(userSearch.value.trim());
        }
    };

    function showTempMessage(text, type='info') {
        let box = document.getElementById('groupTempMsg');
        if (!box) {
            box = document.createElement('div');
            box.id = 'groupTempMsg';
            box.style.position = 'fixed';
            box.style.top = '80px';
            box.style.right = '20px';
            box.style.zIndex = '2000';
            document.body.appendChild(box);
        }
        box.innerHTML = `<div class="alert alert-${type} py-2 px-3" style="background:var(--color-bg-alt);border:1px solid var(--color-border);color:var(--color-text);">${text}</div>`;
        setTimeout(()=>{ box.innerHTML=''; }, 3000);
    }

    // ==== FORM VALIDATION ====
    function validateForm() {
        const name = groupName.value.trim();
        
        if (name.length < 3) {
            alert('Grup adı en az 3 karakter olmalıdır.');
            groupName.focus();
            return false;
        }

        if (selectedUsers.size < MIN_MEMBERS) {
            alert(`En az ${MIN_MEMBERS} üye eklemelisiniz.`);
            userSearch.focus();
            return false;
        }
        if (selectedUsers.size > MAX_MEMBERS) {
            alert(`En fazla ${MAX_MEMBERS} üye ekleyebilirsiniz.`);
            return false;
        }

        return true;
    }

    // ==== FORM SUBMISSION ====
    createGroupForm?.addEventListener('submit', async (e) => {
        e.preventDefault();

        if (!validateForm()) return;

        createGroupBtn.disabled = true;
        createGroupBtn.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span>Oluşturuluyor...';

        try {
            // First upload image if exists
            let groupImageUrl = '';
            if (groupImageFile) {
                try {
                    const imageFormData = new FormData();
                    imageFormData.append('image', groupImageFile);
                    const imageResponse = await fetch('/Chat/UploadGroupImage', { method: 'POST', body: imageFormData });
                    if (imageResponse.ok) {
                        // JSON olmayabilir; güvenli parse
                        let imageResult = {};
                        try { imageResult = await imageResponse.json(); } catch { imageResult = {}; }
                        if (imageResult.success && imageResult.imageUrl) {
                            groupImageUrl = imageResult.imageUrl;
                        }
                    } else {
                        console.warn('Upload failed status:', imageResponse.status);
                    }
                } catch (err) {
                    console.warn('Upload error:', err);
                }
            }

            // Create group
            const groupData = {
                name: groupName.value.trim(),
                description: groupDescription.value.trim(),
                groupImageUrl: groupImageUrl,
                isPrivate: groupPrivate.checked,
                memberIds: Array.from(selectedUsers)
            };

            const response = await fetch('/Chat/CreateGroup', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify(groupData)
            });
            let result = {};
            try { result = await response.json(); } catch { result = { success:false, message:'Sunucu yanıtı okunamadı' }; }

            if (result.success) {
                // Redirect to group chat
                window.location.href = `/Chat/Group/${result.groupId}`;
            } else {
                alert('Grup oluşturulurken hata oluştu: ' + result.message);
            }
        } catch (error) {
            console.error('Group creation error:', error);
            alert('Grup oluşturulurken hata oluştu.');
        } finally {
            createGroupBtn.disabled = false;
            createGroupBtn.innerHTML = '<i class="bi bi-people-fill me-2"></i>Grubu Oluştur';
        }
    });

    // ==== CHARACTER COUNTERS ====
    groupName?.addEventListener('input', () => {
        const length = groupName.value.length;
        const maxLength = 100;
        
        if (length > maxLength) {
            groupName.value = groupName.value.substring(0, maxLength);
        }
    });

    groupDescription?.addEventListener('input', () => {
        const length = groupDescription.value.length;
        const maxLength = 500;
        
        if (length > maxLength) {
            groupDescription.value = groupDescription.value.substring(0, maxLength);
        }
    });

})();
