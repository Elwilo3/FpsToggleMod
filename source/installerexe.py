import os
import sys
import shutil
import subprocess
import time
import configparser
from PyQt5 import QtWidgets, QtCore, QtGui
import zipfile
import psutil

def resource_path(relative_path):
    """Get the absolute path to the resource, works for dev and when using PyInstaller."""
    try:
        base_path = sys._MEIPASS
    except Exception:
        base_path = os.path.abspath(".")
    return os.path.join(base_path, relative_path)

class InstallerApp(QtWidgets.QWidget):
    def __init__(self):
        super().__init__()
        self.game_folder = ""
        self.mod_dll_name = None  # Will be set after detecting the mod DLL
        self.mod_buttons = []
        self.init_ui()
        self.load_config()
        self.detect_mod_name()
        self.update_install_button()
        self.update_mod_buttons()

    def init_ui(self):
        self.setWindowTitle("Get To Work Mod Installer")
        self.resize(600, 600)  # Adjusted size to accommodate the image and new buttons

        self.layout = QtWidgets.QVBoxLayout()

        self.label = QtWidgets.QLabel("Select the 'Get To Work' game folder (You can find it on your Steam library page, as shown below)")
        self.layout.addWidget(self.label)

        image_path = resource_path("instructions.png")
        self.image_label = QtWidgets.QLabel()
        pixmap = QtGui.QPixmap(image_path)

        max_width = 580  # Maximum width we want for the image
        max_height = 300  # Maximum height we want for the image

        # Check if pixmap needs to be scaled down
        if not pixmap.isNull():
            pixmap = pixmap.scaled(max_width, max_height, QtCore.Qt.KeepAspectRatio, QtCore.Qt.SmoothTransformation)

        self.image_label.setPixmap(pixmap)
        self.image_label.setAlignment(QtCore.Qt.AlignCenter)
        self.layout.addWidget(self.image_label)

        # Buttons layout
        buttons_layout = QtWidgets.QHBoxLayout()

        self.select_button = QtWidgets.QPushButton("Select Folder")
        buttons_layout.addWidget(self.select_button)

        self.install_button = QtWidgets.QPushButton("Install Mod")  # Text will be updated later
        buttons_layout.addWidget(self.install_button)

        self.uninstall_all_button = QtWidgets.QPushButton("Uninstall Everything Mod Releated")
        buttons_layout.addWidget(self.uninstall_all_button)

        self.layout.addLayout(buttons_layout)

        # Layout for uninstall mod buttons
        self.mods_layout = QtWidgets.QVBoxLayout()
        self.layout.addLayout(self.mods_layout)

        # Status label
        self.status_label = QtWidgets.QLabel("")
        self.status_label.setWordWrap(True)
        self.status_label.setAlignment(QtCore.Qt.AlignTop)
        self.layout.addWidget(self.status_label)

        self.setLayout(self.layout)

        # Connect signals
        self.select_button.clicked.connect(self.select_folder)
        self.install_button.clicked.connect(self.install_mod)
        self.uninstall_all_button.clicked.connect(self.uninstall_all_mods)

    def load_config(self):
        """Load the saved game folder path from config file."""
        self.config = configparser.ConfigParser()
        self.config_file = os.path.join(os.path.expanduser("~"), ".get_to_work_mod_installer.ini")
        if os.path.exists(self.config_file):
            self.config.read(self.config_file)
            self.game_folder = self.config.get("Settings", "game_folder", fallback="")
            if self.game_folder:
                self.status_label.setText(f"Loaded saved game folder: {self.game_folder}")
        else:
            self.config["Settings"] = {}

    def save_config(self):
        """Save the game folder path to config file."""
        self.config["Settings"]["game_folder"] = self.game_folder
        with open(self.config_file, "w") as configfile:
            self.config.write(configfile)

    def select_folder(self):
        folder = QtWidgets.QFileDialog.getExistingDirectory(self, "Select Game Folder")
        if folder and os.path.basename(folder) == "Get To Work":
            self.game_folder = folder
            self.status_label.setText(f"Selected: {folder}")
            self.save_config()
            self.update_mod_buttons()  # Update mod buttons when folder is changed
        else:
            self.status_label.setText("Invalid folder. Please select the 'Get To Work' folder.")

    def detect_mod_name(self):
        """Detect the mod DLL name from the installer directory."""
        installer_dir = resource_path("")  # Get the directory where the script/resources are
        dll_files = [f for f in os.listdir(installer_dir) if f.lower().endswith('.dll')]
        if dll_files:
            self.mod_dll_name = dll_files[0]  # Take the first DLL found
        else:
            self.mod_dll_name = None
            self.status_label.setText("No mod DLL file found in the installer directory.")

    def update_install_button(self):
        """Update the install button text with the mod name."""
        if self.mod_dll_name:
            mod_name = os.path.splitext(self.mod_dll_name)[0]
            self.install_button.setText(f"Install {mod_name}")
        else:
            self.install_button.setDisabled(True)

    def is_mod_installed(self, mod_dll_name):
        """Check if a specific mod is installed."""
        mod_dll_path = os.path.join(self.game_folder, "BepInEx", "plugins", mod_dll_name)
        return os.path.exists(mod_dll_path)

    def check_initial_files(self):
        required_files = [
            ".doorstop_version",
            "changelog.txt",
            "doorstop_config.ini",
            "winhttp.dll"
        ]

        start_time = time.time()
        while time.time() - start_time < 10:  # Check for 10 seconds
            missing_files = []
            for file in required_files:
                if not os.path.exists(os.path.join(self.game_folder, file)):
                    missing_files.append(file)

            if not os.path.exists(os.path.join(self.game_folder, "BepInEx", "core")):
                missing_files.append("BepInEx/core")

            if not missing_files:  # If no missing files
                return True

            self.status_label.setText(f"Waiting for files: {', '.join(missing_files)}")
            QtWidgets.QApplication.processEvents()
            time.sleep(0.5)

        return False

    def wait_for_bepinex_folders(self):
        required_paths = [
            os.path.join(self.game_folder, "BepInEx", "LogOutput.log"),
            os.path.join(self.game_folder, "BepInEx", "config"),
            os.path.join(self.game_folder, "BepInEx", "cache"),
            os.path.join(self.game_folder, "BepInEx", "plugins"),
            os.path.join(self.game_folder, "BepInEx", "patchers")
        ]

        start_time = time.time()
        while time.time() - start_time < 30:  # 30 seconds timeout
            missing_paths = []
            for path in required_paths:
                if not os.path.exists(path):
                    missing_paths.append(os.path.basename(path))

            if not missing_paths:  # If no missing paths
                return True

            elapsed = int(time.time() - start_time)
            self.status_label.setText(f"Waiting for BepInEx initialization ({elapsed}s)...\nMissing: {', '.join(missing_paths)}")
            QtWidgets.QApplication.processEvents()
            time.sleep(0.5)

        return False

    def install_mod(self):
        if not self.game_folder:
            self.status_label.setText("Please select a valid game folder first.")
            return

        if not self.mod_dll_name:
            self.status_label.setText("No mod DLL found to install.")
            return

        if self.is_mod_installed(self.mod_dll_name):
            self.status_label.setText("Mod is already installed.")
            return

        try:
            # Get resource paths
            zip_file_path = resource_path("BepInEx.zip")
            dll_file_path = resource_path(self.mod_dll_name)  # Use detected mod DLL name

            # Step 1: Check BepInEx installation
            bep_folder = os.path.join(self.game_folder, "BepInEx")
            if not os.path.exists(bep_folder):
                self.status_label.setText("Installing BepInEx...")
                with zipfile.ZipFile(zip_file_path, "r") as zip_ref:
                    zip_ref.extractall(self.game_folder)
                time.sleep(2)

            # Step 2: Check for required files
            self.status_label.setText("Checking for required files...")
            if not self.check_initial_files():
                self.status_label.setText("Error: Required BepInEx files are missing. Please verify the installation.")
                return

            # Step 3: Launch the game
            self.status_label.setText("Launching game to initialize BepInEx...")
            game_path = os.path.join(self.game_folder, "Get To Work.exe")
            if not os.path.exists(game_path):
                self.status_label.setText("Error: Game executable not found.")
                return

            process = subprocess.Popen([game_path])

            # Step 4: Wait for BepInEx folders
            if not self.wait_for_bepinex_folders():
                self.status_label.setText("Error: BepInEx initialization timed out.")
                self.terminate_process("Get To Work.exe")
                return

            # Step 5: Close the game
            self.status_label.setText("Closing game...")
            self.terminate_process("Get To Work.exe")
            time.sleep(2)

            # Step 6: Install the mod
            plugins_folder = os.path.join(bep_folder, "plugins")
            if not os.path.exists(plugins_folder):
                os.makedirs(plugins_folder)

            shutil.copy(dll_file_path, plugins_folder)
            self.status_label.setText("Mod installed successfully!")
            self.update_mod_buttons()  # Update uninstall mod buttons

        except Exception as e:
            self.status_label.setText(f"Error: {str(e)}")

    def uninstall_all_mods(self):
        """Uninstall BepInEx and related files."""
        if not self.game_folder:
            self.status_label.setText("Please select a valid game folder first.")
            return

        # Confirm with the user
        reply = QtWidgets.QMessageBox.question(
            self,
            "Confirm Uninstall",
            "Are you sure you want to uninstall all mods and BepInEx? This will remove BepInEx and related files.",
            QtWidgets.QMessageBox.Yes | QtWidgets.QMessageBox.No,
            QtWidgets.QMessageBox.No,
        )

        if reply == QtWidgets.QMessageBox.Yes:
            try:
                # List of files and folders to remove
                items_to_remove = [
                    os.path.join(self.game_folder, "BepInEx"),
                    os.path.join(self.game_folder, "winhttp.dll"),
                    os.path.join(self.game_folder, "doorstop_config.ini"),
                    os.path.join(self.game_folder, ".doorstop_version"),
                    os.path.join(self.game_folder, "changelog.txt"),
                ]

                self.status_label.setText("Uninstalling all mods and BepInEx...")

                for item in items_to_remove:
                    if os.path.exists(item):
                        if os.path.isfile(item) or os.path.islink(item):
                            os.remove(item)
                        elif os.path.isdir(item):
                            shutil.rmtree(item)

                self.status_label.setText("All mods and BepInEx uninstalled successfully.")
                self.update_mod_buttons()  # Update the mod buttons after uninstallation

            except Exception as e:
                self.status_label.setText(f"Error during uninstallation: {str(e)}")
        else:
            self.status_label.setText("Uninstallation cancelled.")

    def update_mod_buttons(self):
        """Update the uninstall mod buttons based on installed mods."""
        # First, clear existing mod buttons
        for button in self.mod_buttons:
            button.deleteLater()
        self.mod_buttons.clear()

        # If game folder is not set, do nothing
        if not self.game_folder:
            return

        plugins_folder = os.path.join(self.game_folder, "BepInEx", "plugins")
        if not os.path.exists(plugins_folder):
            return

        # List all DLL files in the plugins folder
        mod_files = [f for f in os.listdir(plugins_folder) if f.lower().endswith('.dll')]

        for mod_file in mod_files:
            mod_name = os.path.splitext(mod_file)[0]
            button = QtWidgets.QPushButton(f"Uninstall {mod_name}")
            self.mods_layout.addWidget(button)
            button.clicked.connect(lambda checked, mf=mod_file: self.uninstall_mod(mf))
            self.mod_buttons.append(button)

    def uninstall_mod(self, mod_dll_name):
        """Uninstall only the specified mod DLL from the plugins folder."""
        if not self.game_folder:
            self.status_label.setText("Please select a valid game folder first.")
            return

        mod_dll_path = os.path.join(self.game_folder, "BepInEx", "plugins", mod_dll_name)

        if not os.path.exists(mod_dll_path):
            self.status_label.setText(f"Mod '{mod_dll_name}' is not installed.")
            return

        try:
            os.remove(mod_dll_path)
            self.status_label.setText(f"Mod '{mod_dll_name}' uninstalled successfully.")
            self.update_mod_buttons()  # Update buttons after uninstallation
        except Exception as e:
            self.status_label.setText(f"Error during mod uninstallation: {str(e)}")

    def terminate_process(self, process_name):
        for proc in psutil.process_iter(["name"]):
            if proc.info["name"] == process_name:
                proc.terminate()

def main():
    app = QtWidgets.QApplication([])
    installer = InstallerApp()
    installer.show()
    app.exec_()

if __name__ == "__main__":
    main()
