using UnityEngine;

public class SortingCube : MonoBehaviour {
    public static SortingCube Instance;

    public GameObject teacherView; // Box with holes
    public GameObject studentView; // Plain box

    void Awake() {
        Instance = this;
    }

    public void SetTeacherView(bool isTeacher) {
        teacherView.SetActive(isTeacher);
        studentView.SetActive(!isTeacher);
    }
}
